using DoorMarket.Mobile.Localization;
using System.Collections.ObjectModel;
using DoorMarket.Application.DTOs.Products;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Services;
using DoorMarket.Mobile.Utils;

namespace DoorMarket.Mobile.Pages;

public partial class PromotionsPage : LocalizedContentPage
{
    private readonly ProductsApiClient _productsApi;
    private readonly IServiceProvider _services;
    private bool _loaded;
    private bool _isBusy;

    public ObservableCollection<ProductDto> Promotions { get; } = new();
    public Command BackCommand { get; }

    public new bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public PromotionsPage(ProductsApiClient productsApi, IServiceProvider services)
    {
        InitializeComponent();
        _productsApi = productsApi;
        _services = services;
        BackCommand = new Command(async () => await NavigationHelper.PopAsync());
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
                PageSize: 30);

            var result = await _productsApi.SearchAsync(query, CancellationToken.None);
            Promotions.Clear();
            foreach (var item in result.Items)
            {
                var imageUrl = MediaCatalog.Product(item.MainImageUrl, item.Name, item.CategoryName, item.Id);
                Promotions.Add(ToClientPromotionDisplay(item, imageUrl));
            }
        }
        catch (ApiException ex)
        {
            errors.Add(ex.Message);
        }
        catch
        {
            errors.Add(AppText.Get("LoadPromotionsFailed"));
        }

        IsBusy = false;

        if (errors.Count > 0)
        {
            var message = string.Join(Environment.NewLine, errors.Distinct());
            await DisplayAlert(AppText.Get("ErrorTitle"), message, AppText.Get("Ok"));
        }
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
