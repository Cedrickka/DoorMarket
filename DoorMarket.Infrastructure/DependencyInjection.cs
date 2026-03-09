using DoorMarket.Application.Interfaces.AdminUsers;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Cart;
using DoorMarket.Application.Interfaces.Categories;
using DoorMarket.Application.Interfaces.Orders;
using DoorMarket.Application.Interfaces.Payments;
using DoorMarket.Application.Interfaces.Loyalty;
using DoorMarket.Application.Interfaces.Products;
using DoorMarket.Application.Interfaces.Shops;
using DoorMarket.Infrastructure.AdminUsers;
using DoorMarket.Infrastructure.Auth;
using DoorMarket.Infrastructure.Carts;
using DoorMarket.Infrastructure.Categories;
using DoorMarket.Infrastructure.Orders;
using DoorMarket.Infrastructure.Payments;
using DoorMarket.Infrastructure.Loyalty;
using DoorMarket.Infrastructure.Persistence;
using DoorMarket.Infrastructure.Products;
using DoorMarket.Infrastructure.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;





namespace DoorMarket.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<DoorMarketDbContext>(opt => opt.UseSqlServer(cs));
        services.AddHttpClient();

        // JWT Options
        services.Configure<JwtOptions>(config.GetSection("Jwt"));

        // Auth services
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IShopService, ShopService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IPromoService, PromoService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IStripePaymentService, StripePaymentService>();
        services.AddScoped<IPayPalPaymentService, PayPalPaymentService>();
        services.AddScoped<IMobileMoneyPaymentService, MobileMoneyPaymentService>();
        services.AddScoped<ILoyaltyService, LoyaltyService>();
        services.AddScoped<IAdminUserService, AdminUserService>();






        return services;
    }
}
