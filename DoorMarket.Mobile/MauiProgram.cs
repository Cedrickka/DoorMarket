using System.Net.Http.Headers;
using System.Net;
using System.Reflection;
using DoorMarket.Mobile.Auth;
using DoorMarket.Mobile.Controls;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Pages;
using DoorMarket.Mobile.Services;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Sharpnado.CollectionView;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace DoorMarket.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseSharpnadoCollectionView(loggerEnable: false)
            .UseSkiaSharp();

#if ANDROID
        EntryHandler.Mapper.AppendToMapping("BorderlessEntry", (handler, view) =>
        {
            if (view is BorderlessEntry)
            {
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Colors.Transparent.ToPlatform());
                handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
            }
        });
#endif

        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("DoorMarket.Mobile.appsettings.json"))
        {
            if (stream != null)
            {
                builder.Configuration.AddJsonStream(stream);
            }
        }

        builder.ConfigureFonts(fonts =>
        {
            fonts.AddFont("Inter-Regular.otf", "Inter-Regular");
            fonts.AddFont("Inter-Medium.otf", "Inter-Medium");
            fonts.AddFont("Inter-SemiBold.otf", "Inter-SemiBold");
            fonts.AddFont("Inter-Bold.otf", "Inter-Bold");
            fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            fonts.AddFont("fa-solid-900.ttf", "fa-solid-900");
        });

        builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("Api"));

        builder.Services.AddSingleton<AuthState>();
        builder.Services.AddSingleton<TokenStore>();
        builder.Services.AddSingleton<AuthSession>();
        builder.Services.AddSingleton<LanguageService>();

        builder.Services.AddTransient<AuthMessageHandler>();
        builder.Services.AddTransient<CultureMessageHandler>();

        builder.Services.AddHttpClient("Api", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
            client.BaseAddress = options.GetBaseUri();
            client.Timeout = TimeSpan.FromSeconds(25);
            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        })
        .AddHttpMessageHandler<CultureMessageHandler>()
        .AddHttpMessageHandler<AuthMessageHandler>();

        builder.Services.AddHttpClient("Auth", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
            client.BaseAddress = options.GetBaseUri();
            client.Timeout = TimeSpan.FromSeconds(25);
            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        })
        .AddHttpMessageHandler<CultureMessageHandler>();

        builder.Services.AddSingleton<AuthApiClient>(sp =>
            new AuthApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("Auth")));
        builder.Services.AddSingleton<MeApiClient>(sp =>
            new MeApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api")));
        builder.Services.AddSingleton<AddressApiClient>(sp =>
            new AddressApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api")));
        builder.Services.AddSingleton<CategoriesApiClient>(sp =>
            new CategoriesApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api")));
        builder.Services.AddSingleton<ProductsApiClient>(sp =>
            new ProductsApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api")));
        builder.Services.AddSingleton<ShopsApiClient>(sp =>
            new ShopsApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api")));
        builder.Services.AddSingleton<CartApiClient>(sp =>
            new CartApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api")));
        builder.Services.AddSingleton<OrdersApiClient>(sp =>
            new OrdersApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api")));
        builder.Services.AddSingleton<DeliveryApiClient>(sp =>
            new DeliveryApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api")));
        builder.Services.AddSingleton<PaymentsApiClient>(sp =>
            new PaymentsApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api")));
        builder.Services.AddSingleton<SupportApiClient>(sp =>
            new SupportApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api")));

        builder.Services.AddTransient<SplashPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<CategoriesPage>();
        builder.Services.AddTransient<PromotionsPage>();
        builder.Services.AddTransient<CartPage>();
        builder.Services.AddTransient<OrdersPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<AddressesPage>();
        builder.Services.AddTransient<AddressFormPage>();
        builder.Services.AddTransient<CategoryProductsPage>();
        builder.Services.AddTransient<ProductDetailsPage>();
        builder.Services.AddTransient<ShopsPage>();
        builder.Services.AddTransient<ShopProductsPage>();
        builder.Services.AddTransient<CheckoutPage>();
        builder.Services.AddTransient<OrderDetailsPage>();
        builder.Services.AddTransient<PaymentReturnPage>();
        builder.Services.AddTransient<PaymentsPage>();
        builder.Services.AddTransient<SupportPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

