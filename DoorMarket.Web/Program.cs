using Blazored.LocalStorage;
using Blazored.SessionStorage;
using DoorMarket.Web.Auth;
using DoorMarket.Web.Components;
using DoorMarket.Web.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using System.Globalization;
using System.IO;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("fr-FR"),
        new CultureInfo("en-US")
    };

    options.DefaultRequestCulture = new RequestCulture("fr-FR");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider()
    };
});

// Storage
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddBlazoredSessionStorage();

// Auth services
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<AuthState>();

// HttpClient API + Bearer automatique
var apiBaseUrl = DoorMarket.Web.Utils.ApiBaseUrlResolver.Resolve(builder.Configuration, builder.Environment);
var mediaProxyEnabled = builder.Configuration.GetValue<bool?>("EnableMediaProxy")
    ?? builder.Configuration.GetValue<bool?>("MediaProxy:Enabled")
    ?? true;
var mediaProxyTimeoutSeconds = builder.Configuration.GetValue<int?>("MediaProxy:TimeoutSeconds") ?? 15;
var mediaProxyRetryCount = Math.Clamp(builder.Configuration.GetValue<int?>("MediaProxy:RetryCount") ?? 2, 1, 5);
var mediaProxyMaxBytes = builder.Configuration.GetValue<long?>("MediaProxy:MaxBytes") ?? 8 * 1024 * 1024;
DoorMarket.Web.Utils.MediaCatalog.ConfigureProxy(apiBaseUrl, enableProxy: mediaProxyEnabled);

builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddHttpMessageHandler<CultureHttpMessageHandler>()
.AddHttpMessageHandler(sp =>
    new AuthHttpMessageHandler(sp.GetRequiredService<TokenStore>())
);

// Injecter HttpClient (Api) partout
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));

builder.Services.AddHttpClient("MediaProxy", client =>
{
    client.Timeout = TimeSpan.FromSeconds(mediaProxyTimeoutSeconds);
    client.DefaultRequestVersion = HttpVersion.Version11;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    // Upstream media server sometimes closes keep-alive connections abruptly.
    // Short-lived pooled connections reduce stale-socket "ResponseEnded" failures.
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
    MaxConnectionsPerServer = 32
});


builder.Services.AddScoped<DoorMarket.Web.UI.SnackbarService>();
builder.Services.AddScoped<CartApiClient>();
builder.Services.AddScoped<GuestCartService>();
builder.Services.AddScoped<WishlistService>();
builder.Services.AddScoped<RecentlyViewedService>();
builder.Services.AddTransient<CultureHttpMessageHandler>();

builder.Services.AddMudServices();


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

var requestLocalizationOptions = app.Services
    .GetRequiredService<IOptions<RequestLocalizationOptions>>()
    .Value;

app.UseRequestLocalization(requestLocalizationOptions);
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/media/proxy", async (
    HttpContext context,
    IHttpClientFactory factory,
    ILogger<Program> logger,
    string url,
    CancellationToken ct) =>
{
    if (!mediaProxyEnabled)
    {
        return Results.NotFound();
    }

    if (!Uri.TryCreate(url, UriKind.Absolute, out var target))
    {
        return Results.BadRequest("URL invalide.");
    }

    var apiHost = new Uri(apiBaseUrl).Host;
    if (!string.Equals(target.Host, apiHost, StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest("Host non autorise.");
    }

    if (string.Equals(target.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
    {
        var builder = new UriBuilder(target)
        {
            Scheme = Uri.UriSchemeHttp,
            Port = target.Port == 443 ? -1 : target.Port
        };
        target = builder.Uri;
    }

    if (!string.Equals(target.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest("Seul HTTP est proxifie.");
    }

    var client = factory.CreateClient("MediaProxy");
    Exception? lastException = null;

    for (var attempt = 1; attempt <= mediaProxyRetryCount; attempt++)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, target);
            request.Headers.ConnectionClose = true;
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return Results.StatusCode((int)response.StatusCode);
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                var ext = Path.GetExtension(target.AbsolutePath).ToLowerInvariant();
                var isImageExt = ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".svg";
                if (!isImageExt)
                {
                    return Results.BadRequest("Type non autorise.");
                }
            }

            var declaredLength = response.Content.Headers.ContentLength;
            if (declaredLength.HasValue && declaredLength.Value > mediaProxyMaxBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var buffered = declaredLength.HasValue && declaredLength.Value > 0 && declaredLength.Value <= mediaProxyMaxBytes
                ? new MemoryStream((int)declaredLength.Value)
                : new MemoryStream();

            var totalRead = 0L;
            var bytes = new byte[81920];
            while (true)
            {
                var read = await source.ReadAsync(bytes.AsMemory(0, bytes.Length), ct);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
                if (totalRead > mediaProxyMaxBytes)
                {
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                }

                await buffered.WriteAsync(bytes.AsMemory(0, read), ct);
            }

            if (string.IsNullOrWhiteSpace(contentType))
            {
                contentType = "application/octet-stream";
            }

            context.Response.ContentType = contentType;
            context.Response.ContentLength = buffered.Length;
            context.Response.Headers.CacheControl = "public,max-age=300";
            buffered.Position = 0;
            await buffered.CopyToAsync(context.Response.Body, ct);
            return Results.Empty;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Results.Empty;
        }
        catch (Exception ex) when (ex is HttpIOException or HttpRequestException or IOException or TaskCanceledException)
        {
            lastException = ex;
            if (attempt < mediaProxyRetryCount)
            {
                logger.LogWarning(ex, "Media proxy retry {Attempt}/{RetryCount} for {Target}.", attempt, mediaProxyRetryCount, target);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), CancellationToken.None);
                continue;
            }
        }
    }

    logger.LogWarning(lastException, "Media proxy failed for {Target}.", target);
    return Results.StatusCode(StatusCodes.Status502BadGateway);
});

app.MapGet("/set-culture", (
    HttpContext context,
    string culture,
    string? redirectUri) =>
{
    var normalizedCulture = culture.StartsWith("fr", StringComparison.OrdinalIgnoreCase)
        ? "fr-FR"
        : "en-US";

    var requestCulture = new RequestCulture(normalizedCulture);
    context.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(requestCulture),
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            HttpOnly = false,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax
        });

    var next = string.IsNullOrWhiteSpace(redirectUri) ? "/" : redirectUri;
    if (!Uri.TryCreate(next, UriKind.Relative, out _))
    {
        next = "/";
    }

    return Results.LocalRedirect(next);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
