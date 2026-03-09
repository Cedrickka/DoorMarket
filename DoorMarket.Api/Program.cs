using DoorMarket.Api.Errors;
using DoorMarket.Api.Filters;
using DoorMarket.Api.HealthChecks;
using DoorMarket.Api.Middlewares;
using DoorMarket.Api.Services;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Storage;
using DoorMarket.Infrastructure;
using DoorMarket.Infrastructure.Persistence;
using DoorMarket.Infrastructure.Seeding;
using DoorMarket.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Stripe;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);
ValidateSecurityConfiguration(builder);

var stripeSecret = builder.Configuration["Stripe:SecretKey"];
if (!string.IsNullOrWhiteSpace(stripeSecret))
{
    StripeConfiguration.ApiKey = stripeSecret;
}
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiErrorResultFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<SupportEmailSender>();
builder.Services.AddScoped<IAuthEmailSender, AuthEmailSender>();
builder.Services.AddScoped<IOrderNotificationService, OrderNotificationService>();
builder.Services.AddScoped<IAdminOrderNotificationService, AdminOrderNotificationService>();
builder.Services.AddScoped<IClientOrderNotificationService, ClientOrderNotificationService>();
builder.Services.AddScoped<INotificationReplayService, NotificationReplayService>();
builder.Services.AddScoped<IAbandonedCartRecoveryService, AbandonedCartRecoveryService>();
builder.Services.AddScoped<IOrderPaymentWorkflowService, OrderPaymentWorkflowService>();
builder.Services.AddScoped<FinanceCalculator>();
builder.Services.AddScoped<SearchAnalyticsCalculator>();
builder.Services.AddScoped<CheckoutObservabilityCalculator>();
builder.Services.AddScoped<ProductionObservabilityDashboardService>();
builder.Services.AddScoped<SearchRuleSuggestionService>();
builder.Services.AddScoped<ICheckoutObservabilityService, CheckoutObservabilityService>();
builder.Services.AddScoped<CheckoutAlertingService>();
builder.Services.AddScoped<ICartReminderPushSender, CartReminderPushSender>();
builder.Services.AddScoped<FcmPushProvider>();
builder.Services.AddScoped<ApnsPushProvider>();
builder.Services.AddScoped<IReconciliationCronService, ReconciliationCronService>();
builder.Services.AddTransient<IClaimsTransformation, RoleClaimsTransformation>();
builder.Services.Configure<CartRecoveryOptions>(builder.Configuration.GetSection("CartRecovery"));
builder.Services.Configure<MarketingAutomationOptions>(builder.Configuration.GetSection("MarketingAutomation"));
builder.Services.Configure<CheckoutAlertingOptions>(builder.Configuration.GetSection("CheckoutAlerting"));
builder.Services.Configure<ReconciliationCronOptions>(builder.Configuration.GetSection("ReconciliationCron"));
builder.Services.AddHostedService<AbandonedCartRecoveryWorker>();
builder.Services.AddHostedService<MarketingAutomationWorker>();
builder.Services.AddHostedService<CheckoutAlertingWorker>();
builder.Services.AddHostedService<ReconciliationCronWorker>();
builder.Services.AddHttpClient("PushProvider");

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors", policy =>
    {
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must be configured outside Development.");
        }
    });
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("API is alive."), tags: new[] { "live", "ready" })
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" })
    .AddCheck<SmtpConfigHealthCheck>("smtp", tags: new[] { "ready" })
    .AddCheck<PushConfigHealthCheck>("push", tags: new[] { "ready" })
    .AddCheck<WorkerQueueHealthCheck>("queue", tags: new[] { "ready" });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : (int?)null;

        object? details = null;
        if (retryAfterSeconds.HasValue)
        {
            details = new { retryAfterSeconds = retryAfterSeconds.Value };
        }

        var payload = ApiErrorFactory.FromStatusCode(
            StatusCodes.Status429TooManyRequests,
            context.HttpContext,
            value: "Trop de requetes. Reessayez dans quelques instants.",
            overrideCode: "rate_limited",
            details: details);

        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await context.HttpContext.Response.WriteAsJsonAsync(payload, cancellationToken: token);
    };

    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"auth:{ResolveRateLimitPartition(context)}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("search", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"search:{ResolveRateLimitPartition(context)}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 90,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 20,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));

    options.AddPolicy("checkout", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"checkout:{ResolveRateLimitPartition(context)}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 25,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});


// JWT Auth
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key missing");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "DoorMarket API", Version = "v1" });
    c.CustomSchemaIds(type =>
    {
        var fullName = type.FullName ?? type.Name;
        return fullName
            .Replace("+", ".")
            .Replace("[", string.Empty)
            .Replace("]", string.Empty)
            .Replace(",", "_")
            .Replace("`", "_");
    });

    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    c.AddSecurityRequirement(new()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});
builder.Services.Configure<DoorMarket.Infrastructure.Seeding.SeedUsersOptions>(
    builder.Configuration.GetSection("SeedUsers"));

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DoorMarketDbContext>();
    await DbSeeder.SeedAsync(db);
    await app.Services.SeedUsersAsync();
}
else
{
    app.UseHsts();
}

var useHttpsRedirection = app.Configuration.GetValue<bool?>("UseHttpsRedirection")
    ?? !app.Environment.IsDevelopment();
if (useHttpsRedirection)
{
    app.UseHttpsRedirection();
}

app.UseCors("DefaultCors");

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStatusCodePages(async statusContext =>
{
    var http = statusContext.HttpContext;
    if (!http.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    if (http.Response.StatusCode < 400 || http.Response.StatusCode == StatusCodes.Status204NoContent)
    {
        return;
    }

    if (http.Response.HasStarted)
    {
        return;
    }

    if (http.Response.ContentLength.GetValueOrDefault() > 0)
    {
        return;
    }

    if (!string.IsNullOrWhiteSpace(http.Response.ContentType))
    {
        return;
    }

    var payload = ApiErrorFactory.FromStatusCode(http.Response.StatusCode, http);
    http.Response.ContentType = "application/json; charset=utf-8";
    await http.Response.WriteAsJsonAsync(payload);
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.MapGet("/", () => Results.Ok("DoorMarket API is running."));
app.MapGet("/ping", () => Results.Ok("pong"));
app.MapHealthChecks("/api/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthChecksResponseWriter.WriteJsonAsync
});
app.MapHealthChecks("/api/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthChecksResponseWriter.WriteJsonAsync
});
app.MapControllers();
app.Run();

static string ResolveRateLimitPartition(HttpContext context)
{
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? context.User.FindFirstValue("sub");
    if (!string.IsNullOrWhiteSpace(userId))
    {
        return $"user:{userId}";
    }

    var ip = context.Connection.RemoteIpAddress?.ToString();
    if (!string.IsNullOrWhiteSpace(ip))
    {
        return $"ip:{ip}";
    }

    return "ip:unknown";
}

static void ValidateSecurityConfiguration(WebApplicationBuilder builder)
{
    var cfg = builder.Configuration;
    var env = builder.Environment;
    var errors = new List<string>();

    static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim();
        var markers = new[]
        {
            "CHANGE_ME",
            "MET_UNE_CLE",
            "UNE_CLE_SECRETE",
            "EXAMPLE",
            "YOUR_",
            "TODO"
        };

        return markers.Any(marker => normalized.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    void Require(string key, int minLength = 1)
    {
        var value = cfg[key];
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < minLength || IsPlaceholder(value))
        {
            errors.Add(key);
        }
    }

    if (env.IsProduction())
    {
        Require("ConnectionStrings:DefaultConnection", minLength: 20);
        Require("Jwt:Key", minLength: 32);

        var smtpHost = cfg["Smtp:Host"];
        if (!string.IsNullOrWhiteSpace(smtpHost))
        {
            Require("Smtp:Username");
            Require("Smtp:Password");
            Require("Smtp:FromEmail");
        }

        var supportSmtpHost = cfg["Support:Smtp:Host"];
        if (!string.IsNullOrWhiteSpace(supportSmtpHost))
        {
            Require("Support:Smtp:Username");
            Require("Support:Smtp:Password");
            Require("Support:Smtp:FromEmail");
        }

        var adminSetupEnabled = cfg.GetValue<bool>("AdminSetup:Enabled");
        if (adminSetupEnabled)
        {
            Require("AdminSetup:Key", minLength: 32);
        }

        var stripeSecretKey = cfg["Stripe:SecretKey"];
        if (!string.IsNullOrWhiteSpace(stripeSecretKey))
        {
            Require("Stripe:WebhookSecret", minLength: 16);
        }

        var payPalClientId = cfg["PayPal:ClientId"];
        if (!string.IsNullOrWhiteSpace(payPalClientId))
        {
            Require("PayPal:ClientSecret", minLength: 16);
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Security configuration invalid in Production. Missing/invalid keys: {string.Join(", ", errors.Distinct())}");
        }
    }
}
