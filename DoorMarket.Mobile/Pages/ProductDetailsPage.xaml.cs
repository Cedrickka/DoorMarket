using DoorMarket.Mobile.Localization;
using System.Globalization;
using DoorMarket.Application.DTOs.Products;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Services;
using DoorMarket.Mobile.Utils;

namespace DoorMarket.Mobile.Pages;

public partial class ProductDetailsPage : LocalizedContentPage
{
    private readonly CartApiClient _cartApi;
    private readonly ShopsApiClient _shopsApi;
    private readonly IServiceProvider _services;

    private ProductDto? _product;
    private int _quantity = 1;
    private bool _isAdding;

    public ProductDetailsPage(CartApiClient cartApi, ShopsApiClient shopsApi, IServiceProvider services)
    {
        InitializeComponent();
        _cartApi = cartApi;
        _shopsApi = shopsApi;
        _services = services;
        QuantityLabel.Text = _quantity.ToString(CultureInfo.InvariantCulture);
    }

    public void Load(ProductDto product)
    {
        _product = product;
        var hasPromotion = product.IsPromotionEnabled
            && product.PromotionPrice.HasValue
            && product.PromotionPrice.Value > 0m
            && product.PromotionPrice.Value < product.Price;
        var displayPrice = hasPromotion ? product.PromotionPrice.GetValueOrDefault() : product.EffectivePrice;

        NameLabel.Text = product.Name;
        PriceLabel.Text = string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", displayPrice, product.Currency);
        ShopLabel.Text = string.Format(CultureInfo.CurrentCulture, AppText.Get("ShopLabelFormat"), product.ShopName);
        CategoryLabel.Text = string.Format(CultureInfo.CurrentCulture, AppText.Get("CategoryLabelFormat"), CategoryDisplayName(product));
        StockLabel.Text = product.StockQty > 0
            ? string.Format(CultureInfo.CurrentCulture, AppText.Get("StockLabelFormat"), product.StockQty)
            : AppText.Get("OutOfStock");
        ProductImage.Source = MediaCatalog.Product(product.MainImageUrl, product.Name, product.CategoryName, product.Id);

        DescriptionLabel.Text = string.IsNullOrWhiteSpace(product.Description)
            ? AppText.Get("NoDescription")
            : product.Description;

        if (hasPromotion)
        {
            NormalPriceLabel.Text = string.Format(
                CultureInfo.CurrentCulture,
                AppText.Get("NormalPriceCurrencyFormat"),
                product.Price,
                product.Currency);
            NormalPriceLabel.IsVisible = true;
        }
        else
        {
            NormalPriceLabel.Text = string.Empty;
            NormalPriceLabel.IsVisible = false;
        }

        _quantity = 1;
        QuantityLabel.Text = _quantity.ToString(CultureInfo.InvariantCulture);

        AddToCartButton.IsEnabled = product.StockQty > 0;
    }

    protected override Task RefreshLanguageDataAsync()
    {
        if (_product is not null)
        {
            Load(_product);
        }

        return Task.CompletedTask;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await NavigationHelper.PopAsync();
    }

    private void OnIncreaseQuantity(object sender, EventArgs e)
    {
        if (_product is null)
        {
            return;
        }

        if (_product.StockQty > 0 && _quantity >= _product.StockQty)
        {
            return;
        }

        _quantity += 1;
        QuantityLabel.Text = _quantity.ToString(CultureInfo.InvariantCulture);
    }

    private void OnDecreaseQuantity(object sender, EventArgs e)
    {
        if (_quantity <= 1)
        {
            return;
        }

        _quantity -= 1;
        QuantityLabel.Text = _quantity.ToString(CultureInfo.InvariantCulture);
    }

    private async void OnAddToCartClicked(object sender, EventArgs e)
    {
        if (_isAdding)
        {
            return;
        }

        if (_product is null)
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("ProductNotFound"), AppText.Get("Ok"));
            return;
        }

        if (_product.StockQty <= 0)
        {
            await DisplayAlert(AppText.Get("StockTitle"), AppText.Get("StockOutMessage"), AppText.Get("Ok"));
            return;
        }

        _isAdding = true;
        AddToCartButton.IsEnabled = false;

        try
        {
            await _cartApi.AddItemAsync(_product.Id, _quantity, CancellationToken.None);

            var openCart = await DisplayAlert(
                AppText.Get("CartTitle"),
                AppText.Get("ProductAddedToCart"),
                AppText.Get("ViewCart"),
                AppText.Get("ContinueAction"));

            if (openCart && Shell.Current is not null)
            {
                await Shell.Current.GoToAsync(nameof(CartPage));
            }
        }
        catch (ApiException ex)
        {
            if (IsStockIssue(ex))
            {
                await DisplayAlert(
                    AppText.Get("StockInsufficientTitle"),
                    AppText.Get("StockInsufficientMessage"),
                    AppText.Get("Ok"));
            }
            else
            {
                await DisplayAlert(AppText.Get("ErrorTitle"), ex.Message, AppText.Get("Ok"));
            }
        }
        catch
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("AddToCartFailed"), AppText.Get("Ok"));
        }
        finally
        {
            _isAdding = false;
            AddToCartButton.IsEnabled = _product.StockQty > 0;
        }
    }

    private async void OnViewShopProductsClicked(object sender, EventArgs e)
    {
        if (_product is null)
        {
            return;
        }

        try
        {
            var shop = await _shopsApi.GetByIdAsync(_product.ShopId, CancellationToken.None);
            var page = _services.GetRequiredService<ShopProductsPage>();
            page.Load(shop);
            await NavigationHelper.PushAsync(page);
        }
        catch (ApiException ex)
        {
            await DisplayAlert(AppText.Get("ShopTitle"), ex.Message, AppText.Get("Ok"));
        }
        catch
        {
            await DisplayAlert(AppText.Get("ShopTitle"), AppText.Get("OpenShopFailed"), AppText.Get("Ok"));
        }
    }

    private static bool IsStockIssue(ApiException ex)
        => ex.StatusCode == 409 ||
           string.Equals(ex.ErrorCode, "stock_insufficient", StringComparison.OrdinalIgnoreCase) ||
           ex.Message.Contains("Stock insuffisant", StringComparison.OrdinalIgnoreCase);

    private static bool IsEnglish()
        => string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase);

    private static string CategoryDisplayName(ProductDto product)
        => IsEnglish() && !string.IsNullOrWhiteSpace(product.CategoryNameEn)
            ? product.CategoryNameEn!
            : product.CategoryName;
}
