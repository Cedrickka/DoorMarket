using DoorMarket.Mobile.Localization;
using System.Collections.ObjectModel;
using System.Globalization;
using DoorMarket.Application.DTOs.Categories;
using DoorMarket.Application.DTOs.Products;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Services;
using DoorMarket.Mobile.Utils;

namespace DoorMarket.Mobile.Pages;

public partial class CategoryProductsPage : LocalizedContentPage
{
    private readonly ProductsApiClient _productsApi;
    private readonly CategoriesApiClient _categoriesApi;
    private readonly IServiceProvider _services;
    private CategoryDto? _category;
    private bool _loaded;
    private bool _isBusy;
    private bool _showNoProducts;
    private bool _inStockOnly = true;

    public ObservableCollection<ProductDto> Products { get; } = new();

    public new bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public bool ShowNoProducts
    {
        get => _showNoProducts;
        set
        {
            _showNoProducts = value;
            OnPropertyChanged();
        }
    }

    public CategoryProductsPage(ProductsApiClient productsApi, CategoriesApiClient categoriesApi, IServiceProvider services)
    {
        InitializeComponent();
        _productsApi = productsApi;
        _categoriesApi = categoriesApi;
        _services = services;
        BindingContext = this;
    }

    public void Load(CategoryDto category)
    {
        _category = category;
        TitleLabel.Text = CategoryDisplayName(category);
        _loaded = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;
        await LoadAsync();
    }

    protected override async Task RefreshLanguageDataAsync()
    {
        if (_category is null || IsBusy)
        {
            return;
        }

        try
        {
            var refreshed = await _categoriesApi.GetByIdAsync(_category.Id, CancellationToken.None);
            _category = refreshed;
            TitleLabel.Text = CategoryDisplayName(refreshed);
        }
        catch
        {
            if (_category is not null)
            {
                TitleLabel.Text = CategoryDisplayName(_category);
            }
        }

        await ReloadAsync();
    }

    private async Task LoadAsync()
    {
        if (_category is null)
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("InvalidCategory"), AppText.Get("Ok"));
            return;
        }

        IsBusy = true;
        var errors = new List<string>();
        await LoadProductsAsync(errors);
        IsBusy = false;

        if (errors.Count > 0)
        {
            var message = string.Join(Environment.NewLine, errors.Distinct());
            await DisplayAlert(AppText.Get("ErrorTitle"), message, AppText.Get("Ok"));
        }
    }

    private async Task LoadProductsAsync(ICollection<string> errors)
    {
        if (_category is null) return;

        try
        {
            var query = new ProductQuery(
                ShopId: null,
                CategoryId: _category.Id,
                CountryTag: null,
                City: null,
                Q: string.IsNullOrWhiteSpace(SearchInput?.Text) ? null : SearchInput.Text.Trim(),
                MinPrice: null,
                MaxPrice: null,
                InStockOnly: _inStockOnly,
                ActiveOnly: true,
                Page: 1,
                PageSize: 24);

            var result = await _productsApi.SearchAsync(query, CancellationToken.None);
            Products.Clear();
            foreach (var item in result.Items)
            {
                var imageUrl = MediaCatalog.Product(item.MainImageUrl, item.Name, item.CategoryName, item.Id);
                Products.Add(ToClientPromotionDisplay(item, imageUrl));
            }
            ShowNoProducts = Products.Count == 0;
        }
        catch (ApiException ex)
        {
            errors.Add(ex.Message);
            ShowNoProducts = true;
        }
        catch
        {
            errors.Add(AppText.Get("LoadProductsFailed"));
            ShowNoProducts = true;
        }
    }

    private async Task ReloadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        var errors = new List<string>();
        await LoadProductsAsync(errors);
        IsBusy = false;

        if (errors.Count > 0)
        {
            var message = string.Join(Environment.NewLine, errors.Distinct());
            await DisplayAlert(AppText.Get("ErrorTitle"), message, AppText.Get("Ok"));
        }
    }

    private async void OnSearchCompleted(object sender, EventArgs e)
    {
        await ReloadAsync();
    }

    private async void OnInStockToggled(object sender, ToggledEventArgs e)
    {
        _inStockOnly = e.Value;
        await ReloadAsync();
    }

    private async void OnProductSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is CollectionView collectionView)
            collectionView.SelectedItem = null;

        if (e.CurrentSelection.FirstOrDefault() is not ProductDto product)
            return;

        var page = _services.GetRequiredService<ProductDetailsPage>();
        page.Load(product);
        await NavigationHelper.PushAsync(page);
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await NavigationHelper.PopAsync();
    }

    private static bool IsEnglish()
        => string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase);

    private static string CategoryDisplayName(CategoryDto category)
        => IsEnglish() && !string.IsNullOrWhiteSpace(category.NameEn) ? category.NameEn! : category.Name;

    private static ProductDto ToClientPromotionDisplay(ProductDto product, string imageUrl)
    {
        var hasConfiguredPromotion = product.IsPromotionEnabled
            && product.PromotionPrice.HasValue
            && product.PromotionPrice.Value > 0m
            && product.PromotionPrice.Value < product.Price;

        if (!hasConfiguredPromotion)
        {
            return product with { MainImageUrl = imageUrl };
        }

        return product with
        {
            MainImageUrl = imageUrl,
            EffectivePrice = product.PromotionPrice!.Value,
            HasActivePromotion = true
        };
    }
}







