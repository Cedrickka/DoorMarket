using DoorMarket.Mobile.Localization;
using System.Collections.ObjectModel;
using System.Globalization;
using DoorMarket.Application.DTOs.Categories;
using DoorMarket.Application.DTOs.Products;
using DoorMarket.Application.DTOs.Shops;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Services;
using DoorMarket.Mobile.Utils;

namespace DoorMarket.Mobile.Pages;

public partial class ShopProductsPage : LocalizedContentPage
{
    private readonly ProductsApiClient _productsApi;
    private readonly CategoriesApiClient _categoriesApi;
    private readonly IServiceProvider _services;
    private ShopDto? _shop;
    private bool _loaded;
    private bool _isBusy;
    private bool _showNoProducts;
    private bool _inStockOnly = true;
    private CategoryDto? _selectedCategory;

    public ObservableCollection<ProductDto> Products { get; } = new();
    public ObservableCollection<CategoryDto> CategoryFilters { get; } = new();

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

    public ShopProductsPage(ProductsApiClient productsApi, CategoriesApiClient categoriesApi, IServiceProvider services)
    {
        InitializeComponent();
        _productsApi = productsApi;
        _categoriesApi = categoriesApi;
        _services = services;
        BindingContext = this;
    }

    public void Load(ShopDto shop)
    {
        _shop = shop;
        ShopNameLabel.Text = shop.Name;
        ShopLocationLabel.Text = $"{shop.City} • {shop.CountryTag}";
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
        if (_shop is null || IsBusy)
        {
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_shop is null)
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("ShopNotFound"), AppText.Get("Ok"));
            return;
        }

        IsBusy = true;
        var errors = new List<string>();

        await LoadCategoriesAsync(errors);
        await LoadProductsAsync(errors);

        IsBusy = false;

        if (errors.Count > 0)
        {
            var message = string.Join(Environment.NewLine, errors.Distinct());
            await DisplayAlert(AppText.Get("ErrorTitle"), message, AppText.Get("Ok"));
        }
    }

    private async Task LoadCategoriesAsync(ICollection<string> errors)
    {
        try
        {
            var categories = await _categoriesApi.GetAllAsync(CancellationToken.None);
            CategoryFilters.Clear();
            CategoryFilters.Add(new CategoryDto(Guid.Empty, AppText.Get("AllCategories"), "all", DateTime.UtcNow));
            foreach (var cat in categories.OrderBy(c => c.Name))
                CategoryFilters.Add(LocalizeCategory(cat));
            CategoryFilterCollection.SelectedItem = CategoryFilters.FirstOrDefault();
        }
        catch (ApiException ex)
        {
            errors.Add(ex.Message);
        }
        catch
        {
            errors.Add(AppText.Get("LoadCategoriesFailed"));
        }
    }

    private async Task LoadProductsAsync(ICollection<string> errors)
    {
        if (_shop is null) return;

        try
        {
            var categoryId = _selectedCategory?.Id == Guid.Empty ? null : _selectedCategory?.Id;
            var query = new ProductQuery(
                ShopId: _shop.Id,
                CategoryId: categoryId,
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

    private async void OnCategoryFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedCategory = e.CurrentSelection.FirstOrDefault() as CategoryDto;
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

    private static ProductDto ToClientPromotionDisplay(ProductDto product, string imageUrl)
    {
        var hasConfiguredPromotion = product.IsPromotionEnabled
            && product.PromotionPrice.HasValue
            && product.PromotionPrice.Value > 0m
            && product.PromotionPrice.Value < product.Price;

        if (!hasConfiguredPromotion)
        {
            return LocalizeProductCategory(product with { MainImageUrl = imageUrl });
        }

        return LocalizeProductCategory(product with
        {
            MainImageUrl = imageUrl,
            EffectivePrice = product.PromotionPrice!.Value,
            HasActivePromotion = true
        });
    }

    private static bool IsEnglish()
        => string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase);

    private static CategoryDto LocalizeCategory(CategoryDto category)
        => IsEnglish() && !string.IsNullOrWhiteSpace(category.NameEn)
            ? category with { Name = category.NameEn! }
            : category;

    private static ProductDto LocalizeProductCategory(ProductDto product)
        => IsEnglish() && !string.IsNullOrWhiteSpace(product.CategoryNameEn)
            ? product with { CategoryName = product.CategoryNameEn! }
            : product;
}




