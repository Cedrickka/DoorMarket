using DoorMarket.Mobile.Localization;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using DoorMarket.Application.DTOs.Cart;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Models;
using DoorMarket.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;

namespace DoorMarket.Mobile.Pages;

public partial class CartPage : LocalizedContentPage
{
    private static readonly HashSet<string> AddressIssueCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "delivery_zone_required",
        "delivery_zone_missing",
        "invalid_delivery_zone"
    };

    private static readonly HashSet<string> PaymentIssueCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "prepaid_code_required"
    };

    private static readonly HashSet<string> CatalogIssueCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "empty_cart",
        "product_missing",
        "product_inactive",
        "stock_insufficient",
        "shop_unverified",
        "mixed_currency"
    };

    private readonly CartApiClient _cartApi;
    private readonly IServiceProvider _services;

    private bool _isBusy;
    private decimal _subtotal;
    private decimal _delivery;
    private decimal _discount;
    private string _currency = "USD";
    private string? _appliedPromoCode;
    private string _promoCodeInput = string.Empty;
    private string _promoMessage = string.Empty;
    private Color _promoMessageColor = Colors.Gray;
    private bool _hasPromoMessage;
    private bool _hasReadinessData;
    private string _readinessError = string.Empty;
    private readonly HashSet<string> _issueCodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _warningCodes = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<CartLineViewModel> CartItems { get; } = new();
    public ObservableCollection<string> BlockingIssues { get; } = new();
    public ObservableCollection<string> CheckoutWarnings { get; } = new();
    public Command BackCommand { get; }

    public new bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanCheckout));
            OnPropertyChanged(nameof(CheckoutButtonText));
            OnPropertyChanged(nameof(ShowBusyInline));
        }
    }

    public bool HasItems => CartItems.Count > 0;

    public bool ShowEmptyCart => CartItems.Count == 0;

    public bool CanCheckout => HasItems && !IsBusy && !HasBlockingIssues;

    public string ItemCountLabel => HasItems
        ? (CartItems.Count == 1
            ? AppText.Get("CartItemSingle")
            : string.Format(CultureInfo.CurrentCulture, AppText.Get("CartItemPluralFormat"), CartItems.Count))
        : AppText.Get("CartEmpty");

    public bool HasActivePromo => !string.IsNullOrWhiteSpace(_appliedPromoCode) && _discount > 0m;

    public string ActivePromoBadgeText => HasActivePromo
        ? string.Format(CultureInfo.CurrentCulture, AppText.Get("PromoBadgeFormat"), _appliedPromoCode, FormatMoney(_discount))
        : string.Empty;

    public bool ShowBusyInline => IsBusy && HasItems;

    public string CheckoutButtonText =>
        !HasItems
            ? AppText.Get("CartEmpty")
            : HasBlockingIssues
                ? AppText.Get("CartCheckoutBlockedAction")
                : AppText.Get("CartCheckoutAction");

    public bool HasBlockingIssues => BlockingIssues.Count > 0;

    public bool HasCheckoutWarnings => CheckoutWarnings.Count > 0;

    public bool HasReadinessData => _hasReadinessData;

    public bool ShowCheckoutReady => HasReadinessData && !HasBlockingIssues;

    public bool HasReadinessError => !string.IsNullOrWhiteSpace(_readinessError);

    public bool ShowAddressFixAction => _issueCodes.Overlaps(AddressIssueCodes) || _warningCodes.Overlaps(AddressIssueCodes);

    public bool ShowPaymentFixAction => _issueCodes.Overlaps(PaymentIssueCodes) || _warningCodes.Overlaps(PaymentIssueCodes);

    public bool ShowCatalogFixAction => _issueCodes.Overlaps(CatalogIssueCodes) || _warningCodes.Overlaps(CatalogIssueCodes);

    public bool ShowRetryReadinessAction => HasReadinessError;

    public bool HasFixActions => ShowAddressFixAction || ShowPaymentFixAction || ShowCatalogFixAction || ShowRetryReadinessAction;

    public string ReadinessError
    {
        get => _readinessError;
        set
        {
            if (_readinessError == value)
            {
                return;
            }

            _readinessError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasReadinessError));
            OnPropertyChanged(nameof(ShowRetryReadinessAction));
            OnPropertyChanged(nameof(HasFixActions));
        }
    }

    public string PromoCodeInput
    {
        get => _promoCodeInput;
        set
        {
            if (_promoCodeInput == value)
            {
                return;
            }

            _promoCodeInput = value;
            OnPropertyChanged();
        }
    }

    public string PromoMessage
    {
        get => _promoMessage;
        set
        {
            if (_promoMessage == value)
            {
                return;
            }

            _promoMessage = value;
            OnPropertyChanged();
        }
    }

    public bool HasPromoMessage
    {
        get => _hasPromoMessage;
        set
        {
            if (_hasPromoMessage == value)
            {
                return;
            }

            _hasPromoMessage = value;
            OnPropertyChanged();
        }
    }

    public Color PromoMessageColor
    {
        get => _promoMessageColor;
        set
        {
            if (_promoMessageColor == value)
            {
                return;
            }

            _promoMessageColor = value;
            OnPropertyChanged();
        }
    }

    public string SubtotalText => FormatMoney(_subtotal);

    public string DiscountText => _discount <= 0m
        ? FormatMoney(0m)
        : $"-{FormatMoney(_discount)}";

    public string DeliveryText => FormatMoney(_delivery);

    public string TotalText => FormatMoney(GetTotal());

    public CartPage(CartApiClient cartApi, IServiceProvider services)
    {
        InitializeComponent();
        _cartApi = cartApi;
        _services = services;
        BackCommand = new Command(async () => await NavigateBackAsync());

        CartItems.CollectionChanged += OnCartItemsChanged;
        BlockingIssues.CollectionChanged += OnCheckoutSignalsChanged;
        CheckoutWarnings.CollectionChanged += OnCheckoutSignalsChanged;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadCartAsync();
    }

    protected override async Task RefreshLanguageDataAsync()
    {
        if (IsBusy)
        {
            return;
        }

        await LoadCartAsync();
    }

    private async Task LoadCartAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var cart = await _cartApi.GetMyCartAsync(CancellationToken.None);
            ApplyCart(cart);
            await RefreshPricingAsync(showPromoMessage: false);
        }
        catch (ApiException ex)
        {
            ClearCheckoutReadiness();
            if (IsStockIssue(ex))
            {
                SetPromoMessage(AppText.Get("CartStockInsufficient"), PromoMessageTone.Error);
                return;
            }

            await DisplayAlert(AppText.Get("ErrorTitle"), ex.Message, AppText.Get("Ok"));
        }
        catch
        {
            ClearCheckoutReadiness();
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("CartLoadFailed"), AppText.Get("Ok"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshPricingAsync(bool showPromoMessage)
    {
        _subtotal = CartItems.Sum(i => i.LineTotal);

        if (_subtotal <= 0m)
        {
            _appliedPromoCode = null;
            _discount = 0m;
            _delivery = 0m;
            ClearCheckoutReadiness();
            if (showPromoMessage)
            {
                SetPromoMessage(AppText.Get("CartEmptyMessage"), PromoMessageTone.Info);
            }

            OnTotalsChanged();
            return;
        }

        await RefreshPromoAsync(showPromoMessage);
        await RefreshCheckoutReadinessAsync(showPromoMessage);
        OnTotalsChanged();
    }

    private async Task RefreshPromoAsync(bool showPromoMessage)
    {
        if (string.IsNullOrWhiteSpace(_appliedPromoCode))
        {
            _discount = 0m;
            if (showPromoMessage && string.IsNullOrWhiteSpace(PromoCodeInput))
            {
                SetPromoMessage(string.Empty);
            }

            return;
        }

        try
        {
            var promo = await _cartApi.ApplyPromoAsync(_appliedPromoCode, CancellationToken.None);
            if (!promo.Applied)
            {
                _discount = 0m;
                _appliedPromoCode = null;
                if (showPromoMessage)
                {
                    SetPromoMessage(promo.Message, PromoMessageTone.Error);
                }

                return;
            }

            _discount = promo.Discount;
            _appliedPromoCode = promo.PromoCode ?? _appliedPromoCode;
            if (showPromoMessage)
            {
                SetPromoMessage(promo.Message, PromoMessageTone.Success);
            }
        }
        catch (ApiException ex)
        {
            _discount = 0m;
            if (showPromoMessage)
            {
                SetPromoMessage(ex.Message, PromoMessageTone.Error);
            }
        }
        catch
        {
            _discount = 0m;
            if (showPromoMessage)
            {
                SetPromoMessage(AppText.Get("PromoApplyFailed"), PromoMessageTone.Error);
            }
        }
    }

    private async Task RefreshCheckoutReadinessAsync(bool showPromoMessage)
    {
        try
        {
            var readiness = await _cartApi.PreCheckoutAsync(
                new PreCheckoutRequest(
                    DeliveryZoneId: null,
                    PromoCode: _appliedPromoCode,
                    PaymentProvider: "PayPal",
                    PrepaidCardCode: null,
                    RequireDeliveryZone: false),
                CancellationToken.None);

            _hasReadinessData = true;
            ReadinessError = string.Empty;

            _subtotal = readiness.Subtotal;
            _discount = readiness.Discount;
            _delivery = readiness.DeliveryFee;
            _currency = string.IsNullOrWhiteSpace(readiness.Currency) ? _currency : readiness.Currency;

            BlockingIssues.Clear();
            _issueCodes.Clear();
            foreach (var issue in readiness.BlockingIssues)
            {
                BlockingIssues.Add(FormatIssue(issue));
                if (!string.IsNullOrWhiteSpace(issue.Code))
                {
                    _issueCodes.Add(issue.Code);
                }
            }

            CheckoutWarnings.Clear();
            _warningCodes.Clear();
            foreach (var warning in readiness.Warnings)
            {
                CheckoutWarnings.Add(FormatIssue(warning));
                if (!string.IsNullOrWhiteSpace(warning.Code))
                {
                    _warningCodes.Add(warning.Code);
                }
            }

            if (showPromoMessage && HasBlockingIssues)
            {
                SetPromoMessage(AppText.Get("CartCheckoutBlockedHint"), PromoMessageTone.Error);
            }
            else if (showPromoMessage && HasCheckoutWarnings)
            {
                SetPromoMessage(AppText.Get("CartCheckoutWarningHint"), PromoMessageTone.Info);
            }
            else if (showPromoMessage && _discount <= 0m)
            {
                SetPromoMessage(AppText.Get("CartDeliveryHint"), PromoMessageTone.Info);
            }
        }
        catch (ApiException ex)
        {
            _hasReadinessData = false;
            ReadinessError = ex.Message;
            BlockingIssues.Clear();
            CheckoutWarnings.Clear();
            _issueCodes.Clear();
            _warningCodes.Clear();
            _delivery = 0m;

            if (showPromoMessage)
            {
                SetPromoMessage(AppText.Get("CartPreCheckoutUnavailableHint"), PromoMessageTone.Info);
            }
        }
        catch
        {
            _hasReadinessData = false;
            ReadinessError = AppText.Get("CartPreCheckoutUnavailableHint");
            BlockingIssues.Clear();
            CheckoutWarnings.Clear();
            _issueCodes.Clear();
            _warningCodes.Clear();
            _delivery = 0m;

            if (showPromoMessage)
            {
                SetPromoMessage(AppText.Get("CartPreCheckoutUnavailableHint"), PromoMessageTone.Info);
            }
        }

        OnPropertyChanged(nameof(HasReadinessData));
        OnPropertyChanged(nameof(ShowCheckoutReady));
        OnPropertyChanged(nameof(ShowAddressFixAction));
        OnPropertyChanged(nameof(ShowPaymentFixAction));
        OnPropertyChanged(nameof(ShowCatalogFixAction));
        OnPropertyChanged(nameof(ShowRetryReadinessAction));
        OnPropertyChanged(nameof(HasFixActions));
        OnPropertyChanged(nameof(CanCheckout));
        OnPropertyChanged(nameof(CheckoutButtonText));
    }

    private void ClearCheckoutReadiness()
    {
        _hasReadinessData = false;
        ReadinessError = string.Empty;
        BlockingIssues.Clear();
        CheckoutWarnings.Clear();
        _issueCodes.Clear();
        _warningCodes.Clear();
        OnPropertyChanged(nameof(HasReadinessData));
        OnPropertyChanged(nameof(ShowCheckoutReady));
        OnPropertyChanged(nameof(ShowAddressFixAction));
        OnPropertyChanged(nameof(ShowPaymentFixAction));
        OnPropertyChanged(nameof(ShowCatalogFixAction));
        OnPropertyChanged(nameof(ShowRetryReadinessAction));
        OnPropertyChanged(nameof(HasFixActions));
        OnPropertyChanged(nameof(CanCheckout));
        OnPropertyChanged(nameof(CheckoutButtonText));
    }

    private static string FormatIssue(CheckoutIssueDto issue)
    {
        if (!string.IsNullOrWhiteSpace(issue.ProductName))
        {
            return $"{issue.Message} ({issue.ProductName})";
        }

        return issue.Message;
    }

    private void ApplyCart(CartDto cart)
    {
        _currency = string.IsNullOrWhiteSpace(cart.Currency) ? "USD" : cart.Currency;

        CartItems.Clear();
        foreach (var item in cart.Items)
        {
            CartItems.Add(new CartLineViewModel
            {
                Id = item.Id,
                ProductId = item.ProductId,
                Name = item.ProductName,
                Quantity = item.Qty,
                UnitPrice = item.UnitPrice,
                LineTotal = item.LineTotal,
                Currency = item.Currency,
                ImageUrl = string.IsNullOrWhiteSpace(item.MainImageUrl) ? "doormarket_icon_256.png" : item.MainImageUrl
            });
        }

        _subtotal = cart.Subtotal;
        OnTotalsChanged();
    }

    private void OnCartItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(ShowEmptyCart));
        OnPropertyChanged(nameof(CanCheckout));
        OnPropertyChanged(nameof(ItemCountLabel));
        OnPropertyChanged(nameof(CheckoutButtonText));
        OnPropertyChanged(nameof(ShowBusyInline));
        OnPropertyChanged(nameof(ShowCheckoutReady));
    }

    private void OnCheckoutSignalsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasBlockingIssues));
        OnPropertyChanged(nameof(HasCheckoutWarnings));
        OnPropertyChanged(nameof(ShowCheckoutReady));
        OnPropertyChanged(nameof(ShowAddressFixAction));
        OnPropertyChanged(nameof(ShowPaymentFixAction));
        OnPropertyChanged(nameof(ShowCatalogFixAction));
        OnPropertyChanged(nameof(ShowRetryReadinessAction));
        OnPropertyChanged(nameof(HasFixActions));
        OnPropertyChanged(nameof(CanCheckout));
        OnPropertyChanged(nameof(CheckoutButtonText));
    }

    private void OnTotalsChanged()
    {
        OnPropertyChanged(nameof(SubtotalText));
        OnPropertyChanged(nameof(DiscountText));
        OnPropertyChanged(nameof(DeliveryText));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(HasActivePromo));
        OnPropertyChanged(nameof(ActivePromoBadgeText));
    }

    private void SetPromoMessage(string message, PromoMessageTone tone = PromoMessageTone.Info)
    {
        var resources = Microsoft.Maui.Controls.Application.Current!.Resources;
        PromoMessage = message;
        PromoMessageColor = string.IsNullOrWhiteSpace(message)
            ? ResolveColor(resources, "DmTextSecondary", "DmTextSecondaryLight")
            : tone switch
            {
                PromoMessageTone.Success => ResolveColor(resources, "SuccessGreen", "DmSuccessLight"),
                PromoMessageTone.Error => ResolveColor(resources, "ErrorRed", "DmErrorLight"),
                _ => ResolveColor(resources, "DmTextSecondary", "DmTextSecondaryLight")
            };
        HasPromoMessage = !string.IsNullOrWhiteSpace(message);
    }

    private static Color ResolveColor(ResourceDictionary resources, string key, string fallbackKey)
    {
        if (resources.TryGetValue(key, out var value) && value is Color color)
        {
            return color;
        }

        if (resources.TryGetValue(fallbackKey, out var fallback) && fallback is Color fallbackColor)
        {
            return fallbackColor;
        }

        return Colors.Gray;
    }

    private decimal GetTotal()
    {
        var subtotalAfterDiscount = Math.Max(0m, _subtotal - _discount);
        return subtotalAfterDiscount + _delivery;
    }

    private string FormatMoney(decimal value)
        => string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", value, _currency);

    private async Task RunCartMutationAsync(Func<Task<CartDto>> mutation)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var cart = await mutation();
            ApplyCart(cart);
            await RefreshPricingAsync(showPromoMessage: false);
        }
        catch (ApiException ex)
        {
            if (IsStockIssue(ex))
            {
                SetPromoMessage(AppText.Get("CartStockInsufficientUpdate"), PromoMessageTone.Error);
                return;
            }

            await DisplayAlert(AppText.Get("ErrorTitle"), ex.Message, AppText.Get("Ok"));
        }
        catch
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("CartOperationFailed"), AppText.Get("Ok"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnIncreaseQtyClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not CartLineViewModel item)
        {
            return;
        }

        await RunCartMutationAsync(() => _cartApi.UpdateItemAsync(item.Id, item.Quantity + 1, CancellationToken.None));
    }

    private async void OnDecreaseQtyClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not CartLineViewModel item)
        {
            return;
        }

        if (item.Quantity <= 1)
        {
            await RunCartMutationAsync(() => _cartApi.RemoveItemAsync(item.Id, CancellationToken.None));
            return;
        }

        await RunCartMutationAsync(() => _cartApi.UpdateItemAsync(item.Id, item.Quantity - 1, CancellationToken.None));
    }

    private async void OnRemoveItemClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not CartLineViewModel item)
        {
            return;
        }

        await RunCartMutationAsync(() => _cartApi.RemoveItemAsync(item.Id, CancellationToken.None));
    }

    private async void OnRemoveItemTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not CartLineViewModel item)
        {
            return;
        }

        await RunCartMutationAsync(() => _cartApi.RemoveItemAsync(item.Id, CancellationToken.None));
    }

    private async void OnApplyPromoClicked(object sender, EventArgs e)
    {
        if (IsBusy)
        {
            return;
        }

        if (!HasItems)
        {
            SetPromoMessage(AppText.Get("CartEmptyMessage"), PromoMessageTone.Info);
            return;
        }

        var promoCode = (PromoCodeInput ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(promoCode))
        {
            _appliedPromoCode = null;
            _discount = 0m;
            await RefreshPricingAsync(showPromoMessage: true);
            SetPromoMessage(AppText.Get("PromoEmpty"), PromoMessageTone.Error);
            return;
        }

        IsBusy = true;

        try
        {
            _appliedPromoCode = promoCode;
            await RefreshPricingAsync(showPromoMessage: true);
            PromoCodeInput = _appliedPromoCode ?? string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnCheckoutClicked(object sender, EventArgs e)
    {
        if (IsBusy)
        {
            return;
        }

        if (!HasItems)
        {
            await DisplayAlert(AppText.Get("CartTitle"), AppText.Get("CartEmptyMessage"), AppText.Get("Ok"));
            return;
        }

        if (HasBlockingIssues)
        {
            var details = string.Join(Environment.NewLine, BlockingIssues.Take(4).Select(x => $"- {x}"));
            var message = string.IsNullOrWhiteSpace(details)
                ? AppText.Get("CartCheckoutBlockedHint")
                : $"{AppText.Get("CartCheckoutBlockedHint")}{Environment.NewLine}{Environment.NewLine}{details}";

            await DisplayAlert(
                AppText.Get("CartCheckoutBlockedTitle"),
                message,
                AppText.Get("Ok"));
            return;
        }

        try
        {
            var checkoutPage = _services.GetRequiredService<CheckoutPage>();
            checkoutPage.Load(_appliedPromoCode);
            await NavigationHelper.PushAsync(checkoutPage);
        }
        catch
        {
            await DisplayAlert(AppText.Get("NavigationTitle"), AppText.Get("CheckoutOpenFailed"), AppText.Get("Ok"));
        }
    }

    private async void OnFixAddressClicked(object sender, EventArgs e)
    {
        try
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync(nameof(AddressesPage));
                return;
            }

            var page = _services.GetRequiredService<AddressesPage>();
            await NavigationHelper.PushAsync(page);
        }
        catch
        {
            await DisplayAlert(AppText.Get("NavigationTitle"), AppText.Get("NavigationOpenAddressesFailed"), AppText.Get("Ok"));
        }
    }

    private async void OnFixPaymentsClicked(object sender, EventArgs e)
    {
        try
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync(nameof(PaymentsPage));
                return;
            }

            var page = _services.GetRequiredService<PaymentsPage>();
            await NavigationHelper.PushAsync(page);
        }
        catch
        {
            await DisplayAlert(AppText.Get("NavigationTitle"), AppText.Get("NavigationOpenPaymentsFailed"), AppText.Get("Ok"));
        }
    }

    private async void OnFixCatalogClicked(object sender, EventArgs e)
    {
        try
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("//categories");
                return;
            }

            await NavigationHelper.PopAsync();
        }
        catch
        {
            await DisplayAlert(AppText.Get("NavigationTitle"), AppText.Get("CatalogOpenFailed"), AppText.Get("Ok"));
        }
    }

    private async void OnRetryReadinessClicked(object sender, EventArgs e)
    {
        await LoadCartAsync();
    }

    private async void OnExploreCatalogClicked(object sender, EventArgs e)
    {
        try
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("//categories");
                return;
            }

            await NavigationHelper.PopAsync();
        }
        catch
        {
            await DisplayAlert(AppText.Get("NavigationTitle"), AppText.Get("CatalogOpenFailed"), AppText.Get("Ok"));
        }
    }

    private async Task NavigateBackAsync()
    {
        try
        {
            if (Navigation.NavigationStack.Count > 1)
            {
                await NavigationHelper.PopAsync();
                return;
            }

            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("//home");
            }
        }
        catch
        {
            await DisplayAlert(AppText.Get("NavigationTitle"), AppText.Get("NavigationBackFailed"), AppText.Get("Ok"));
        }
    }

    private static bool IsStockIssue(ApiException ex)
        => ex.StatusCode == 409 ||
           string.Equals(ex.ErrorCode, "stock_insufficient", StringComparison.OrdinalIgnoreCase) ||
           ex.Message.Contains("Stock insuffisant", StringComparison.OrdinalIgnoreCase);

    private enum PromoMessageTone
    {
        Info,
        Success,
        Error
    }
}
