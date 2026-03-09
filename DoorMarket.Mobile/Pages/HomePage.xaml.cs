using DoorMarket.Mobile.Localization;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using DoorMarket.Application.DTOs.Categories;
using DoorMarket.Application.DTOs.Products;
using DoorMarket.Application.DTOs.Shops;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Services;
using DoorMarket.Mobile.Utils;
using System.Globalization;

namespace DoorMarket.Mobile.Pages;

public partial class HomePage : LocalizedContentPage
{
    private readonly CategoriesApiClient _categoriesApi;
    private readonly ProductsApiClient _productsApi;
    private readonly ShopsApiClient _shopsApi;
    private readonly MeApiClient _meApi;
    private readonly IServiceProvider _services;
    private bool _loaded;
    private bool _isBusy;
    private bool _showNoCategories;
    private bool _showNoProducts;
    private bool _showNoPromotions;
    private bool _showPromotionHero;
    private bool _showNoShops;
    private string? _displayName;
    private string _greetingTitle = AppText.Get("HomeGreeting");
    private double _productsGridHeight = 220d;
    private string _promoHeroImageUrl = string.Empty;
    private string _promoHeroTitle = AppText.Get("PromotionsNow");
    private string _promoHeroSubtitle = string.Empty;

    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public ObservableCollection<ProductDto> Products { get; } = new();
    public ObservableCollection<ProductDto> Promotions { get; } = new();
    public ObservableCollection<ShopDto> Shops { get; } = new();
    public Command CartCommand { get; }

    public string GreetingTitle
    {
        get => _greetingTitle;
        set
        {
            if (_greetingTitle == value)
            {
                return;
            }

            _greetingTitle = value;
            OnPropertyChanged();
        }
    }

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

    public bool ShowNoProducts
    {
        get => _showNoProducts;
        set
        {
            _showNoProducts = value;
            OnPropertyChanged();
        }
    }

    public bool ShowNoPromotions
    {
        get => _showNoPromotions;
        set
        {
            _showNoPromotions = value;
            OnPropertyChanged();
        }
    }

    public bool ShowPromotionHero
    {
        get => _showPromotionHero;
        set
        {
            if (_showPromotionHero == value)
            {
                return;
            }

            _showPromotionHero = value;
            OnPropertyChanged();
        }
    }

    public bool ShowNoShops
    {
        get => _showNoShops;
        set
        {
            _showNoShops = value;
            OnPropertyChanged();
        }
    }

    public double ProductsGridHeight
    {
        get => _productsGridHeight;
        set
        {
            if (Math.Abs(_productsGridHeight - value) < 0.1d)
            {
                return;
            }

            _productsGridHeight = value;
            OnPropertyChanged();
        }
    }

    public string PromoHeroImageUrl
    {
        get => _promoHeroImageUrl;
        set
        {
            if (_promoHeroImageUrl == value)
            {
                return;
            }

            _promoHeroImageUrl = value;
            OnPropertyChanged();
        }
    }

    public string PromoHeroTitle
    {
        get => _promoHeroTitle;
        set
        {
            if (_promoHeroTitle == value)
            {
                return;
            }

            _promoHeroTitle = value;
            OnPropertyChanged();
        }
    }

    public string PromoHeroSubtitle
    {
        get => _promoHeroSubtitle;
        set
        {
            if (_promoHeroSubtitle == value)
            {
                return;
            }

            _promoHeroSubtitle = value;
            OnPropertyChanged();
        }
    }

    public HomePage(
        CategoriesApiClient categoriesApi,
        ProductsApiClient productsApi,
        ShopsApiClient shopsApi,
        MeApiClient meApi,
        IServiceProvider services)
    {
        InitializeComponent();
        _categoriesApi = categoriesApi;
        _productsApi = productsApi;
        _shopsApi = shopsApi;
        _meApi = meApi;
        _services = services;
        CartCommand = new Command(async () => await NavigateToCartAsync());
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
        var errors = new ConcurrentBag<string>();

        await Task.WhenAll(
            LoadGreetingAsync(),
            LoadCategoriesAsync(errors),
            LoadShopsAsync(errors),
            LoadPromotionsAsync(errors),
            LoadProductsAsync(SearchInput?.Text, errors));

        IsBusy = false;

        if (!errors.IsEmpty)
        {
            var message = string.Join(Environment.NewLine, errors.Distinct());
            await DisplayAlert(AppText.Get("ErrorTitle"), message, AppText.Get("Ok"));
        }
    }

    private async Task LoadCategoriesAsync(ConcurrentBag<string> errors)
    {
        try
        {
            var categories = await _categoriesApi.GetAllAsync(CancellationToken.None);
            Categories.Clear();
            foreach (var cat in categories.OrderBy(c => c.Name))
                Categories.Add(LocalizeCategory(cat));
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

    private async Task LoadShopsAsync(ConcurrentBag<string> errors)
    {
        try
        {
            var query = new ShopQuery(null, null, null, 1, 6, true);
            var result = await _shopsApi.SearchAsync(query, null, true, CancellationToken.None);
            Shops.Clear();
            foreach (var item in result.Items)
            {
                var imageUrl = MediaCatalog.Shop(item.ImageUrl, item.Name, item.Id);
                Shops.Add(item with { ImageUrl = imageUrl });
            }
            ShowNoShops = Shops.Count == 0;
        }
        catch (ApiException ex)
        {
            errors.Add(ex.Message);
            ShowNoShops = true;
        }
        catch
        {
            errors.Add(AppText.Get("LoadShopsFailed"));
            ShowNoShops = true;
        }
    }

    private async Task LoadProductsAsync(string? queryText, ConcurrentBag<string> errors)
    {
        try
        {
            var query = new ProductQuery(
                ShopId: null,
                CategoryId: null,
                CountryTag: null,
                City: null,
                Q: string.IsNullOrWhiteSpace(queryText) ? null : queryText.Trim(),
                MinPrice: null,
                MaxPrice: null,
                InStockOnly: true,
                ActiveOnly: true,
                Page: 1,
                PageSize: 16);

            var result = await _productsApi.SearchAsync(query, CancellationToken.None);
            Products.Clear();
            foreach (var item in result.Items)
            {
                var imageUrl = MediaCatalog.Product(item.MainImageUrl, item.Name, item.CategoryName, item.Id);
                Products.Add(ToClientPromotionDisplay(item, imageUrl));
            }
            ShowNoProducts = Products.Count == 0;
            RecalculateProductsGridHeight();
        }
        catch (ApiException ex)
        {
            errors.Add(ex.Message);
            ShowNoProducts = true;
            RecalculateProductsGridHeight();
        }
        catch
        {
            errors.Add(AppText.Get("LoadProductsFailed"));
            ShowNoProducts = true;
            RecalculateProductsGridHeight();
        }
    }

    private async Task LoadPromotionsAsync(ConcurrentBag<string> errors)
    {
        try
        {
            var query = new ProductQuery(
                ShopId: null,
                CategoryId: null,
                CountryTag: null,
                City: null,
                Q: null,
                MinPrice: null,
                MaxPrice: null,
                InStockOnly: false,
                ActiveOnly: true,
                PromotedOnly: true,
                Page: 1,
                PageSize: 12);

            var result = await _productsApi.SearchAsync(query, CancellationToken.None);
            var promoItems = result.Items;
            if (promoItems.Count == 0)
            {
                var fallbackQuery = new ProductQuery(
                    ShopId: null,
                    CategoryId: null,
                    CountryTag: null,
                    City: null,
                    Q: null,
                    MinPrice: null,
                    MaxPrice: null,
                    InStockOnly: false,
                    ActiveOnly: true,
                    PromotedOnly: false,
                    Page: 1,
                    PageSize: 60);

                var fallbackResult = await _productsApi.SearchAsync(fallbackQuery, CancellationToken.None);
                promoItems = fallbackResult.Items
                    .Where(p => p.IsPromotionEnabled && p.PromotionPrice.HasValue && p.PromotionPrice.Value > 0m && p.PromotionPrice.Value < p.Price)
                    .ToList();
            }

            Promotions.Clear();
            foreach (var item in promoItems)
            {
                var imageUrl = MediaCatalog.Product(item.MainImageUrl, item.Name, item.CategoryName, item.Id);
                Promotions.Add(ToClientPromotionDisplay(item, imageUrl));
            }

            ShowNoPromotions = Promotions.Count == 0;
            if (Promotions.Count == 0)
            {
                ShowPromotionHero = false;
                PromoHeroImageUrl = string.Empty;
                PromoHeroTitle = AppText.Get("PromotionsNow");
                PromoHeroSubtitle = string.Empty;
                return;
            }

            ShowPromotionHero = true;
            var hero = Promotions[0];
            PromoHeroImageUrl = hero.MainImageUrl ?? string.Empty;
            PromoHeroTitle = string.IsNullOrWhiteSpace(hero.Name)
                ? AppText.Get("PromotionsNow")
                : hero.Name;
            PromoHeroSubtitle = hero.PromotionPercent.HasValue
                ? string.Format(AppText.Get("PromoHeroPercentFormat"), hero.PromotionPercent.Value, hero.ShopName)
                : string.Format(AppText.Get("PromoHeroDefaultFormat"), hero.ShopName);
        }
        catch (ApiException ex)
        {
            errors.Add(ex.Message);
            ShowNoPromotions = true;
            ShowPromotionHero = false;
            PromoHeroImageUrl = string.Empty;
        }
        catch
        {
            errors.Add(AppText.Get("LoadPromotionsFailed"));
            ShowNoPromotions = true;
            ShowPromotionHero = false;
            PromoHeroImageUrl = string.Empty;
        }
    }

    private void RecalculateProductsGridHeight()
    {
        var rows = Math.Max(1, (int)Math.Ceiling(Products.Count / 4d));
        ProductsGridHeight = (rows * 132d) + ((rows - 1) * 10d);
    }

    private async void OnCategoryTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not CategoryDto category)
            return;

        var page = _services.GetRequiredService<CategoryProductsPage>();
        page.Load(category);
        await NavigationHelper.PushAsync(page);
    }

    private async void OnProductTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not ProductDto product)
            return;

        var page = _services.GetRequiredService<ProductDetailsPage>();
        page.Load(product);
        await NavigationHelper.PushAsync(page);
    }

    private async void OnShopSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is CollectionView collectionView)
            collectionView.SelectedItem = null;

        if (e.CurrentSelection.FirstOrDefault() is not ShopDto shop)
            return;

        var page = _services.GetRequiredService<ShopProductsPage>();
        page.Load(shop);
        await NavigationHelper.PushAsync(page);
    }

    private async void OnShopProductsClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ShopDto shop })
        {
            return;
        }

        var page = _services.GetRequiredService<ShopProductsPage>();
        page.Load(shop);
        await NavigationHelper.PushAsync(page);
    }

    private async void OnAllCategoriesClicked(object sender, EventArgs e)
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("//categories");
        }
    }

    private async void OnAllShopsClicked(object sender, EventArgs e)
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync(nameof(ShopsPage));
        }
    }

    private async Task LoadGreetingAsync()
    {
        try
        {
            var me = await _meApi.GetMeAsync(CancellationToken.None);
            var displayName = BuildDisplayNameFromEmail(me.Email);
            _displayName = displayName;
            GreetingTitle = string.IsNullOrWhiteSpace(displayName)
                ? AppText.Get("HomeGreeting")
                : string.Format(AppText.Get("HomeGreetingWithName"), displayName);
        }
        catch
        {
            _displayName = null;
            GreetingTitle = AppText.Get("HomeGreeting");
        }
    }

    private static string BuildDisplayNameFromEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var localPart = email.Split('@')[0].Trim();
        if (string.IsNullOrWhiteSpace(localPart))
        {
            return string.Empty;
        }

        var cleaned = localPart
            .Replace('.', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return localPart;
        }

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleaned.ToLowerInvariant());
    }

    private static bool IsEnglish()
        => string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase);

    private static CategoryDto LocalizeCategory(CategoryDto category)
        => IsEnglish() && !string.IsNullOrWhiteSpace(category.NameEn)
            ? category with { Name = category.NameEn! }
            : category;

    private async void OnAllProductsClicked(object sender, EventArgs e)
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("//categories");
        }
    }

    private async void OnAllPromotionsClicked(object sender, EventArgs e)
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync(nameof(PromotionsPage));
        }
    }

    private async void OnSearchCompleted(object sender, EventArgs e)
    {
        if (IsBusy) return;
        IsBusy = true;
        var errors = new ConcurrentBag<string>();
        await LoadProductsAsync(SearchInput?.Text, errors);
        IsBusy = false;

        if (!errors.IsEmpty)
        {
            var message = string.Join(Environment.NewLine, errors.Distinct());
            await DisplayAlert(AppText.Get("ErrorTitle"), message, AppText.Get("Ok"));
        }
    }

    private async Task NavigateToCartAsync()
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync(nameof(CartPage));
        }
    }

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
