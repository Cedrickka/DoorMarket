using DoorMarket.Mobile.Localization;
using System.Collections.ObjectModel;
using System.Globalization;
using DoorMarket.Application.DTOs.Categories;
using DoorMarket.Application.DTOs.Shops;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Services;
using DoorMarket.Mobile.Utils;

namespace DoorMarket.Mobile.Pages;

public partial class ShopsPage : LocalizedContentPage
{
    private readonly ShopsApiClient _shopsApi;
    private readonly CategoriesApiClient _categoriesApi;
    private readonly IServiceProvider _services;
    private bool _loaded;
    private bool _isBusy;
    private bool _showNoShops;
    private CategoryDto? _selectedCategory;

    public ObservableCollection<ShopDto> Shops { get; } = new();
    public ObservableCollection<CategoryDto> CategoryFilters { get; } = new();
    public ObservableCollection<string> Countries { get; } = new();
    public Command CartCommand { get; }

    public string SelectedCountry { get; set; }

    public bool RecommendedOnly { get; set; }

    public new bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
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

    public ShopsPage(ShopsApiClient shopsApi, CategoriesApiClient categoriesApi, IServiceProvider services)
    {
        InitializeComponent();
        _shopsApi = shopsApi;
        _categoriesApi = categoriesApi;
        _services = services;
        CartCommand = new Command(async () => await NavigateToCartAsync());
        SelectedCountry = AllCountriesLabel;
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

        Countries.Clear();
        SelectedCountry = AllCountriesLabel;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        var errors = new List<string>();

        await LoadCategoriesAsync(errors);
        await LoadShopsAsync(errors);

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
            _selectedCategory = CategoryFilters.FirstOrDefault();
            CategoryFilterCollection.SelectedItem = _selectedCategory;
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

    private async Task LoadShopsAsync(ICollection<string> errors)
    {
        try
        {
            var country = SelectedCountry == AllCountriesLabel ? null : SelectedCountry;
            var query = new ShopQuery(country, null, string.IsNullOrWhiteSpace(SearchInput?.Text) ? null : SearchInput.Text.Trim(), 1, 24, true);
            var categoryId = _selectedCategory?.Id == Guid.Empty ? null : _selectedCategory?.Id;
            var recommended = RecommendedOnly ? true : (bool?)null;

            var result = await _shopsApi.SearchAsync(query, categoryId, recommended, CancellationToken.None);
            Shops.Clear();
            foreach (var item in result.Items)
            {
                var imageUrl = MediaCatalog.Shop(item.ImageUrl, item.Name, item.Id);
                Shops.Add(item with { ImageUrl = imageUrl });
            }
            ShowNoShops = Shops.Count == 0;

            UpdateCountriesFromShops();
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

    private void UpdateCountriesFromShops()
    {
        if (Countries.Count > 0) return;
        Countries.Add(AllCountriesLabel);
        foreach (var country in Shops.Select(s => s.CountryTag).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c))
            Countries.Add(country);
        if (CountryPicker.SelectedIndex < 0)
        {
            CountryPicker.SelectedIndex = 0;
            SelectedCountry = AllCountriesLabel;
        }
    }

    private async void OnSearchCompleted(object sender, EventArgs e)
    {
        await ReloadAsync();
    }

    private async void OnCountryChanged(object sender, EventArgs e)
    {
        if (CountryPicker.SelectedItem is string country)
            SelectedCountry = country;
        await ReloadAsync();
    }

    private async void OnRecommendedToggled(object sender, ToggledEventArgs e)
    {
        RecommendedOnly = e.Value;
        await ReloadAsync();
    }

    private async void OnCategoryFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedCategory = e.CurrentSelection.FirstOrDefault() as CategoryDto;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        var errors = new List<string>();
        await LoadShopsAsync(errors);
        IsBusy = false;

        if (errors.Count > 0)
        {
            var message = string.Join(Environment.NewLine, errors.Distinct());
            await DisplayAlert(AppText.Get("ErrorTitle"), message, AppText.Get("Ok"));
        }
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

    private async Task NavigateToCartAsync()
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync(nameof(CartPage));
        }
    }

    private static bool IsEnglish()
        => string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase);

    private static CategoryDto LocalizeCategory(CategoryDto category)
        => IsEnglish() && !string.IsNullOrWhiteSpace(category.NameEn)
            ? category with { Name = category.NameEn! }
            : category;

    private static string AllCountriesLabel => AppText.Get("AllCountries");
}



