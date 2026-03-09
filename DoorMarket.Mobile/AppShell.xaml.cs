using DoorMarket.Mobile.Pages;

namespace DoorMarket.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(AddressesPage), typeof(AddressesPage));
        Routing.RegisterRoute(nameof(AddressFormPage), typeof(AddressFormPage));
        Routing.RegisterRoute(nameof(CategoryProductsPage), typeof(CategoryProductsPage));
        Routing.RegisterRoute(nameof(PromotionsPage), typeof(PromotionsPage));
        Routing.RegisterRoute(nameof(ProductDetailsPage), typeof(ProductDetailsPage));
        Routing.RegisterRoute(nameof(ShopsPage), typeof(ShopsPage));
        Routing.RegisterRoute(nameof(ShopProductsPage), typeof(ShopProductsPage));
        Routing.RegisterRoute(nameof(CartPage), typeof(CartPage));
        Routing.RegisterRoute(nameof(OrdersPage), typeof(OrdersPage));
        Routing.RegisterRoute(nameof(CheckoutPage), typeof(CheckoutPage));
        Routing.RegisterRoute(nameof(OrderDetailsPage), typeof(OrderDetailsPage));
        Routing.RegisterRoute(nameof(PaymentReturnPage), typeof(PaymentReturnPage));
        Routing.RegisterRoute(nameof(PaymentsPage), typeof(PaymentsPage));
        Routing.RegisterRoute(nameof(SupportPage), typeof(SupportPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
    }
}
