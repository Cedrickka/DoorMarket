using DoorMarket.Mobile.Localization;
using System.Collections.ObjectModel;
using System.Globalization;
using DoorMarket.Application.DTOs.Categories;
using DoorMarket.Application.DTOs.Products;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Services;
using DoorMarket.Mobile.Utils;

namespace DoorMarket.Mobile.Pages;

public partial class CategoriesPage : LocalizedContentPage
{
    private readonly CategoriesApiClient _categoriesApi;
    private readonly ProductsApiClient _productsApi;
    private readonly IServiceProvider _services;
    private bool _loaded;
    private bool _isBusy;
    private bool _showNoCategories;

    public ObservableCollection<CategoryDto> Categories { get; } = new();
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

    public bool ShowNoCategories
    {
        get => _showNoCategories;
        set
        {
            _showNoCategories = value;
            OnPropertyChanged();
        }
    }

    public CategoriesPage(CategoriesApiClient categoriesApi, ProductsApiClient productsApi, IServiceProvider services)
    {
        InitializeComponent();
        _categoriesApi = categoriesApi;
        _productsApi = productsApi;
        _services = services;
        BindingContext = this;
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
        if (IsBusy)
        {
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
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
            var items = await _categoriesApi.GetAllAsync(CancellationToken.None);
            Categories.Clear();
            foreach (var item in items.OrderBy(c => c.Name))
                Categories.Add(LocalizeCategory(item));
            ShowNoCategories = Categories.Count == 0;
        }
        catch (ApiException ex)
        {
            errors.Add(ex.Message);
            ShowNoCategories = true;
        }
        catch
        {
            errors.Add(AppText.Get("LoadCategoriesFailed"));
            ShowNoCategories = true;
        }
    }

    private async Task LoadProductsAsync(ICollection<string> errors)
    {
        try
        {
            var query = new ProductQuery(
                ShopId: null,
                CategoryId: null,
                CountryTag: null,
                City: null,
                Q: string.IsNullOrWhiteSpace(SearchInput?.Text) ? null : SearchInput.Text.Trim(),
                MinPrice: null,
                MaxPrice: null,
                InStockOnly: true,
                ActiveOnly: true,
                Page: 1,
                PageSize: 12);

            var result = await _productsApi.SearchAsync(query, CancellationToken.None);
            Products.Clear();
            foreach (var item in result.Items)
            {
                var imageUrl = MediaCatalog.Product(item.MainImageUrl, item.Name, item.CategoryName, item.Id);
                Products.Add(ToClientPromotionDisplay(item, imageUrl));
            }
        }
        catch (ApiException ex)
        {
            errors.Add(ex.Message);
        }
        catch
        {
            errors.Add(AppText.Get("LoadProductsFailed"));
        }
    }

    private async Task ReloadProductsAsync()
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

    private async void OnCategoryTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not CategoryDto category)
            return;

        var page = _services.GetRequiredService<CategoryProductsPage>();
        page.Load(category);
        await NavigationHelper.PushAsync(page);
    }

    private async void OnProductSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is CollectionView collectionView)
            collectionView.SelectedItem = null;

        if (e.CurrentSelection.FirstOrDefault() is not ProductDto product)
            return;

        await OpenProductDetailsAsync(product);
    }

    private async void OnProductTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not ProductDto product)
            return;

        await OpenProductDetailsAsync(product);
    }

    private async Task OpenProductDetailsAsync(ProductDto product)
    {
        var page = _services.GetRequiredService<ProductDetailsPage>();
        page.Load(product);
        await NavigationHelper.PushAsync(page);
    }

    private async void OnSearchCompleted(object sender, EventArgs e)
    {
        await ReloadProductsAsync();
    }

    private static bool IsEnglish()
        => string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase);

    private static CategoryDto LocalizeCategory(CategoryDto category)
        => IsEnglish() && !string.IsNullOrWhiteSpace(category.NameEn)
            ? category with { Name = category.NameEn! }
            : category;

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




