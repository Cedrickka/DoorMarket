using DoorMarket.Mobile.Localization;
using System.Collections.ObjectModel;
using System.Globalization;
using DoorMarket.Application.DTOs.Me;
using DoorMarket.Application.DTOs.Orders;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace DoorMarket.Mobile.Pages;

public partial class CheckoutPage : LocalizedContentPage
{
    private const string PreferredPaymentKey = "dm_preferred_payment";
    private readonly CartApiClient _cartApi;
    private readonly AddressApiClient _addressApi;
    private readonly OrdersApiClient _ordersApi;
    private readonly DeliveryApiClient _deliveryApi;
    private readonly PaymentsApiClient _paymentsApi;
    private readonly IServiceProvider _services;

    private bool _isBusy;
    private bool _isSubmittingOrder;
    private bool _hasItems;
    private bool _showNoAddresses;
    private AddressDto? _selectedAddress;

    private string? _promoCode;
    private decimal _subtotal;
    private decimal _discount;
    private decimal _delivery;
    private string _currency = "USD";
    private string _selectedPaymentMethod = "PayPal";
    private string _prepaidCardCode = string.Empty;

    private string _headerTotal = "0 USD";
    private string _placeOrderButtonText = string.Empty;
    private string _placeOrderHintText = string.Empty;
    private Color _placeOrderHintColor = Colors.Gray;

    public ObservableCollection<AddressDto> Addresses { get; } = new();

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
            OnPropertyChanged(nameof(CanPlaceOrder));
            RefreshFooterState();
        }
    }

    public bool ShowNoAddresses
    {
        get => _showNoAddresses;
        set
        {
            if (_showNoAddresses == value)
            {
                return;
            }

            _showNoAddresses = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanPlaceOrder));
            RefreshFooterState();
        }
    }

    public AddressDto? SelectedAddress
    {
        get => _selectedAddress;
        set
        {
            if (_selectedAddress == value)
            {
                return;
            }

            _selectedAddress = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedAddress));
            OnPropertyChanged(nameof(HasSelectedZone));
            OnPropertyChanged(nameof(NeedsZoneSelection));
            OnPropertyChanged(nameof(SelectedAddressName));
            OnPropertyChanged(nameof(SelectedAddressPhone));
            OnPropertyChanged(nameof(SelectedAddressLine));
            OnPropertyChanged(nameof(SelectedAddressCityCountry));
            OnPropertyChanged(nameof(SelectedDeliveryZoneLabel));
            OnPropertyChanged(nameof(CanPlaceOrder));
            RefreshFooterState();
            _ = RecalculateDeliveryFromSelectedAddressAsync();
        }
    }

    public bool HasSelectedAddress => SelectedAddress is not null;
    public bool HasSelectedZone => SelectedAddress?.DeliveryZoneId.HasValue == true;
    public bool NeedsZoneSelection => HasSelectedAddress && !HasSelectedZone;

    public string SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set
        {
            if (_selectedPaymentMethod == value)
            {
                return;
            }

            _selectedPaymentMethod = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPayPalSelected));
            OnPropertyChanged(nameof(IsPrepaidSelected));
            OnPropertyChanged(nameof(PaymentMethodLabel));
            OnPropertyChanged(nameof(PayPalCardBackgroundColor));
            OnPropertyChanged(nameof(PayPalCardBorderColor));
            OnPropertyChanged(nameof(PayPalCardTextColor));
            OnPropertyChanged(nameof(PrepaidCardBackgroundColor));
            OnPropertyChanged(nameof(PrepaidCardBorderColor));
            OnPropertyChanged(nameof(PrepaidCardTextColor));
            OnPropertyChanged(nameof(PrepaidCodeHintText));
            OnPropertyChanged(nameof(PrepaidCodeHintColor));
            OnPropertyChanged(nameof(CardPreviewBrand));
            OnPropertyChanged(nameof(CardPreviewMaskedNumber));
            OnPropertyChanged(nameof(CardPreviewBackgroundColor));
            OnPropertyChanged(nameof(CardPreviewBorderColor));
            OnPropertyChanged(nameof(CardPreviewTextColor));
            OnPropertyChanged(nameof(CardPreviewSecondaryTextColor));
            OnPropertyChanged(nameof(CanPlaceOrder));
            RefreshFooterState();
        }
    }

    public string PrepaidCardCode
    {
        get => _prepaidCardCode;
        set
        {
            if (_prepaidCardCode == value)
            {
                return;
            }

            _prepaidCardCode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PrepaidCodeHintText));
            OnPropertyChanged(nameof(PrepaidCodeHintColor));
            OnPropertyChanged(nameof(CardPreviewBrand));
            OnPropertyChanged(nameof(CardPreviewMaskedNumber));
            OnPropertyChanged(nameof(CardPreviewBackgroundColor));
            OnPropertyChanged(nameof(CardPreviewBorderColor));
            OnPropertyChanged(nameof(CardPreviewTextColor));
            OnPropertyChanged(nameof(CardPreviewSecondaryTextColor));
            OnPropertyChanged(nameof(CanPlaceOrder));
            RefreshFooterState();
        }
    }

    public bool IsPayPalSelected => string.Equals(SelectedPaymentMethod, "PayPal", StringComparison.OrdinalIgnoreCase);

    public bool IsPrepaidSelected => string.Equals(SelectedPaymentMethod, "PrepaidCard", StringComparison.OrdinalIgnoreCase);

    public string PaymentMethodLabel => IsPayPalSelected ? AppText.Get("PayPalLabel") : AppText.Get("PrepaidCardLabel");

    public Color PayPalCardBackgroundColor =>
        IsPayPalSelected
            ? ResolveColor("DoorBlue", "DmHeaderBgLight")
            : ResolveColor("DmSurface", "DmSurfaceLight");

    public Color PayPalCardBorderColor =>
        IsPayPalSelected
            ? ResolveColor("DoorBlue", "DmHeaderBgLight")
            : ResolveColor("DmBorder", "DmBorderLight");

    public Color PayPalCardTextColor =>
        IsPayPalSelected
            ? Colors.White
            : ResolveColor("DoorBlue", "DmTextPrimaryLight");

    public Color PrepaidCardBackgroundColor =>
        IsPrepaidSelected
            ? ResolveColor("DoorOrange", "DmWarningLight")
            : ResolveColor("DmSurface", "DmSurfaceLight");

    public Color PrepaidCardBorderColor =>
        IsPrepaidSelected
            ? ResolveColor("DoorOrange", "DmWarningLight")
            : ResolveColor("DmBorder", "DmBorderLight");

    public Color PrepaidCardTextColor =>
        IsPrepaidSelected
            ? Colors.White
            : ResolveColor("DoorBlue", "DmTextPrimaryLight");

    public string CardPreviewBrand
    {
        get
        {
            if (!IsPrepaidSelected)
            {
                return string.Empty;
            }

            var brand = DetermineCardBrand(GetCardDigits(PrepaidCardCode));
            return brand switch
            {
                "VISA" => "VISA",
                "MASTERCARD" => "MASTERCARD",
                _ => "PREPAID"
            };
        }
    }

    public string CardPreviewMaskedNumber
    {
        get
        {
            var digits = GetCardDigits(PrepaidCardCode);
            if (string.IsNullOrWhiteSpace(digits))
            {
                return "**** **** **** ****";
            }

            var last4 = digits.Length <= 4 ? digits : digits[^4..];
            return $"**** **** **** {last4.PadLeft(4, '*')}";
        }
    }

    public string CardPreviewHolder => AppText.Get("CardHolderLabel");

    public Color CardPreviewBackgroundColor
    {
        get
        {
            return DetermineCardBrand(GetCardDigits(PrepaidCardCode)) switch
            {
                "VISA" => ResolveColor("DoorBlue", "DmHeaderBgLight"),
                "MASTERCARD" => ResolveColor("DoorOrange", "DmWarningLight"),
                _ => ResolveColor("DmSurfaceAlt", "DmSurfaceAltLight")
            };
        }
    }

    public Color CardPreviewBorderColor
    {
        get
        {
            return DetermineCardBrand(GetCardDigits(PrepaidCardCode)) switch
            {
                "VISA" => ResolveColor("DoorBlue", "DmHeaderBgLight"),
                "MASTERCARD" => ResolveColor("DoorOrange", "DmWarningLight"),
                _ => ResolveColor("DmBorder", "DmBorderLight")
            };
        }
    }

    public Color CardPreviewTextColor
    {
        get
        {
            return DetermineCardBrand(GetCardDigits(PrepaidCardCode)) switch
            {
                "VISA" => Colors.White,
                "MASTERCARD" => Colors.White,
                _ => ResolveColor("DoorBlue", "DmTextPrimaryLight")
            };
        }
    }

    public Color CardPreviewSecondaryTextColor
    {
        get
        {
            return DetermineCardBrand(GetCardDigits(PrepaidCardCode)) switch
            {
                "VISA" => Color.FromArgb("#DCE9F8"),
                "MASTERCARD" => Color.FromArgb("#FFF1E4"),
                _ => ResolveColor("DmTextSecondary", "DmTextSecondaryLight")
            };
        }
    }

    public string PrepaidCodeHintText
    {
        get
        {
            if (!IsPrepaidSelected)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(PrepaidCardCode))
            {
                return AppText.Get("PrepaidCodeRequiredHint");
            }

            return IsValidPrepaidInput(PrepaidCardCode)
                ? AppText.Get("PrepaidCodeValidHint")
                : AppText.Get("PrepaidCodeInvalidHint");
        }
    }

    public Color PrepaidCodeHintColor
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PrepaidCardCode))
            {
                return ResolveColor("DmTextSecondary", "DmTextSecondaryLight");
            }

            return IsValidPrepaidInput(PrepaidCardCode)
                ? ResolveColor("SuccessGreen", "DmSuccessLight")
                : ResolveColor("ErrorRed", "DmErrorLight");
        }
    }

    public bool CanPlaceOrder =>
        !IsBusy &&
        _hasItems &&
        HasSelectedAddress &&
        HasSelectedZone &&
        (!IsPrepaidSelected || IsValidPrepaidInput(PrepaidCardCode));

    public string HeaderTotal
    {
        get => _headerTotal;
        set
        {
            if (_headerTotal == value)
            {
                return;
            }

            _headerTotal = value;
            OnPropertyChanged();
        }
    }

    public string PlaceOrderButtonText
    {
        get => _placeOrderButtonText;
        set
        {
            if (_placeOrderButtonText == value)
            {
                return;
            }

            _placeOrderButtonText = value;
            OnPropertyChanged();
        }
    }

    public string PlaceOrderHintText
    {
        get => _placeOrderHintText;
        set
        {
            if (_placeOrderHintText == value)
            {
                return;
            }

            _placeOrderHintText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPlaceOrderHint));
        }
    }

    public bool HasPlaceOrderHint => !string.IsNullOrWhiteSpace(PlaceOrderHintText);

    public Color PlaceOrderHintColor
    {
        get => _placeOrderHintColor;
        set
        {
            if (_placeOrderHintColor == value)
            {
                return;
            }

            _placeOrderHintColor = value;
            OnPropertyChanged();
        }
    }

    public string SelectedAddressName => SelectedAddress?.FullName ?? string.Empty;

    public string SelectedAddressPhone => SelectedAddress?.Phone ?? string.Empty;

    public string SelectedAddressLine
    {
        get
        {
            if (SelectedAddress is null)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(SelectedAddress.District))
            {
                return SelectedAddress.Street;
            }

            return $"{SelectedAddress.Street}, {SelectedAddress.District}";
        }
    }

    public string SelectedAddressCityCountry
    {
        get
        {
            if (SelectedAddress is null)
            {
                return string.Empty;
            }

            return $"{SelectedAddress.City}, {SelectedAddress.Country}";
        }
    }

    public string SelectedDeliveryZoneLabel
        => SelectedAddress is null
            ? string.Empty
             : string.IsNullOrWhiteSpace(SelectedAddress.DeliveryZoneName)
                ? AppText.Get("ZoneRequiredShort")
                : SelectedAddress.DeliveryZoneName;

    public bool HasPromoCode => !string.IsNullOrWhiteSpace(_promoCode);

    public string PromoCodeLabel => _promoCode ?? string.Empty;

    public string SubtotalText => FormatMoney(_subtotal);

    public string DiscountText => _discount <= 0m
        ? FormatMoney(0m)
        : $"-{FormatMoney(_discount)}";

    public string DeliveryText => FormatMoney(_delivery);

    public string TotalText
    {
        get
        {
            var subtotalAfterDiscount = Math.Max(0m, _subtotal - _discount);
            return FormatMoney(subtotalAfterDiscount + _delivery);
        }
    }

    public CheckoutPage(
        CartApiClient cartApi,
        AddressApiClient addressApi,
        OrdersApiClient ordersApi,
        DeliveryApiClient deliveryApi,
        PaymentsApiClient paymentsApi,
        IServiceProvider services)
    {
        InitializeComponent();
        _cartApi = cartApi;
        _addressApi = addressApi;
        _ordersApi = ordersApi;
        _deliveryApi = deliveryApi;
        _paymentsApi = paymentsApi;
        _services = services;

        BindingContext = this;
        PlaceOrderButtonText = AppText.Get("PlaceOrderDefaultAction");
        RefreshFooterState();
    }

    public void Load(string? promoCode)
    {
        _promoCode = string.IsNullOrWhiteSpace(promoCode)
            ? null
            : promoCode.Trim().ToUpperInvariant();

        var preferredMethod = Preferences.Default.Get(PreferredPaymentKey, "PayPal");
        SelectedPaymentMethod = preferredMethod is "PrepaidCard" ? "PrepaidCard" : "PayPal";
        PrepaidCardCode = string.Empty;

        OnPropertyChanged(nameof(HasPromoCode));
        OnPropertyChanged(nameof(PromoCodeLabel));
        RefreshFooterState();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    protected override Task RefreshLanguageDataAsync()
    {
        RefreshFooterState();
        OnPropertyChanged(nameof(PaymentMethodLabel));
        OnPropertyChanged(nameof(CardPreviewHolder));
        OnPropertyChanged(nameof(PrepaidCodeHintText));
        OnPropertyChanged(nameof(SelectedDeliveryZoneLabel));
        return Task.CompletedTask;
    }

    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            await LoadAddressesAsync();
            await LoadCartSummaryAsync();
        }
        catch (ApiException ex)
        {
            if (IsStockIssue(ex))
            {
                PlaceOrderHintText = AppText.Get("CheckoutStockInsufficientHint");
                PlaceOrderHintColor = ResolveColor("ErrorRed", "DmErrorLight");
                await DisplayAlert(AppText.Get("StockInsufficientTitle"), AppText.Get("StockInsufficientAlert"), AppText.Get("Ok"));
            }
            else
            {
                await DisplayAlert(AppText.Get("ErrorTitle"), ex.Message, AppText.Get("Ok"));
            }
        }
        catch
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("CheckoutPrepareFailed"), AppText.Get("Ok"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCartSummaryAsync()
    {
        var cart = await _cartApi.GetMyCartAsync(CancellationToken.None);

        _hasItems = cart.Items.Count > 0;
        _currency = string.IsNullOrWhiteSpace(cart.Currency) ? "USD" : cart.Currency;
        _subtotal = cart.Subtotal;

        if (_hasItems && !string.IsNullOrWhiteSpace(_promoCode))
        {
            var promo = await _cartApi.ApplyPromoAsync(_promoCode, CancellationToken.None);
            if (promo.Applied)
            {
                _discount = promo.Discount;
                _promoCode = promo.PromoCode ?? _promoCode;
            }
            else
            {
                _discount = 0m;
                _promoCode = null;
            }
        }
        else
        {
            _discount = 0m;
        }

        if (_hasItems && HasSelectedZone)
        {
            var quote = await _deliveryApi.GetQuoteAsync(_subtotal, _currency, SelectedAddress?.DeliveryZoneId, CancellationToken.None);
            _delivery = quote.DeliveryFee;
            if (!string.IsNullOrWhiteSpace(quote.Currency))
            {
                _currency = quote.Currency;
            }
        }
        else
        {
            _delivery = 0m;
        }

        OnPropertyChanged(nameof(SubtotalText));
        OnPropertyChanged(nameof(DiscountText));
        OnPropertyChanged(nameof(DeliveryText));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(HasPromoCode));
        OnPropertyChanged(nameof(PromoCodeLabel));
        OnPropertyChanged(nameof(CanPlaceOrder));

        HeaderTotal = TotalText;
        RefreshFooterState();
    }

    private async Task LoadAddressesAsync()
    {
        var addresses = await _addressApi.GetAddressesAsync(CancellationToken.None);

        Addresses.Clear();
        foreach (var address in addresses.OrderByDescending(a => a.IsDefault).ThenBy(a => a.Label))
        {
            Addresses.Add(address);
        }

        ShowNoAddresses = Addresses.Count == 0;

        if (Addresses.Count == 0)
        {
            SelectedAddress = null;
            return;
        }

        if (SelectedAddress is not null)
        {
            var sameAddress = Addresses.FirstOrDefault(a => a.Id == SelectedAddress.Id);
            if (sameAddress is not null)
            {
                SelectedAddress = sameAddress;
                return;
            }
        }

        SelectedAddress = Addresses.FirstOrDefault(a => a.IsDefault) ?? Addresses[0];
    }

    private string FormatMoney(decimal value)
        => string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", value, _currency);

    private async Task RecalculateDeliveryFromSelectedAddressAsync()
    {
        if (!_hasItems)
        {
            return;
        }

        if (!HasSelectedZone)
        {
            _delivery = 0m;
            OnPropertyChanged(nameof(DeliveryText));
            OnPropertyChanged(nameof(TotalText));
            HeaderTotal = TotalText;
            return;
        }

        try
        {
            var quote = await _deliveryApi.GetQuoteAsync(_subtotal, _currency, SelectedAddress?.DeliveryZoneId, CancellationToken.None);
            _delivery = quote.DeliveryFee;
            OnPropertyChanged(nameof(DeliveryText));
            OnPropertyChanged(nameof(TotalText));
            HeaderTotal = TotalText;
        }
        catch
        {
            // Ignore transient quote failures on selection changes.
        }
    }

    private void RefreshFooterState()
    {
        if (IsBusy)
        {
            PlaceOrderButtonText = AppText.Get("CheckoutProcessing");
            PlaceOrderHintText = AppText.Get("CheckoutProcessingHint");
            PlaceOrderHintColor = ResolveColor("DmTextSecondary", "DmTextSecondaryLight");
            return;
        }

        if (!_hasItems)
        {
            PlaceOrderButtonText = AppText.Get("CheckoutCartEmptyTitle");
            PlaceOrderHintText = AppText.Get("CheckoutCartEmptyHint");
            PlaceOrderHintColor = ResolveColor("ErrorRed", "DmErrorLight");
            return;
        }

        if (!HasSelectedAddress)
        {
            PlaceOrderButtonText = AppText.Get("CheckoutAddressRequiredTitle");
            PlaceOrderHintText = AppText.Get("CheckoutAddressRequiredHint");
            PlaceOrderHintColor = ResolveColor("ErrorRed", "DmErrorLight");
            return;
        }

        if (!HasSelectedZone)
        {
            PlaceOrderButtonText = AppText.Get("CheckoutZoneRequiredTitle");
            PlaceOrderHintText = AppText.Get("CheckoutZoneRequiredHint");
            PlaceOrderHintColor = ResolveColor("ErrorRed", "DmErrorLight");
            return;
        }

        if (IsPrepaidSelected && string.IsNullOrWhiteSpace(PrepaidCardCode))
        {
            PlaceOrderButtonText = AppText.Get("CheckoutCodeRequiredTitle");
            PlaceOrderHintText = AppText.Get("CheckoutCodeRequiredHint");
            PlaceOrderHintColor = ResolveColor("ErrorRed", "DmErrorLight");
            return;
        }

        if (IsPrepaidSelected && !IsValidPrepaidInput(PrepaidCardCode))
        {
            PlaceOrderButtonText = AppText.Get("CheckoutCardInvalidTitle");
            PlaceOrderHintText = AppText.Get("CheckoutCardInvalidHint");
            PlaceOrderHintColor = ResolveColor("ErrorRed", "DmErrorLight");
            return;
        }

        PlaceOrderButtonText = IsPayPalSelected ? AppText.Get("CheckoutPayPalAction") : AppText.Get("CheckoutPrepaidAction");
        PlaceOrderHintText = IsPayPalSelected
            ? AppText.Get("CheckoutPayPalHint")
            : AppText.Get("CheckoutPrepaidHint");
        PlaceOrderHintColor = ResolveColor("SuccessGreen", "DmSuccessLight");
    }

    private static Color ResolveColor(string key, string fallbackKey)
    {
        var resources = Microsoft.Maui.Controls.Application.Current!.Resources;
        if (resources.TryGetValue(key, out var value) && value is Color color)
        {
            return color;
        }

        if (resources.TryGetValue(fallbackKey, out var fallbackValue) && fallbackValue is Color fallbackColor)
        {
            return fallbackColor;
        }

        return Colors.Gray;
    }

    private async void OnManageAddressesClicked(object sender, EventArgs e)
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync(nameof(AddressesPage));
        }
    }

    private void OnPayPalSelected(object sender, TappedEventArgs e)
    {
        SelectedPaymentMethod = "PayPal";
    }

    private void OnPrepaidSelected(object sender, TappedEventArgs e)
    {
        SelectedPaymentMethod = "PrepaidCard";
    }

    private async void OnPlaceOrderClicked(object sender, EventArgs e)
    {
        if (IsBusy || _isSubmittingOrder)
        {
            return;
        }

        if (!_hasItems)
        {
            await DisplayAlert(AppText.Get("CartTitle"), AppText.Get("CheckoutCartEmptyAlert"), AppText.Get("Ok"));
            return;
        }

        if (SelectedAddress is null)
        {
            await DisplayAlert(AppText.Get("CheckoutAddressRequiredTitle"), AppText.Get("CheckoutAddressRequiredAlert"), AppText.Get("Ok"));
            return;
        }

        if (!HasSelectedZone)
        {
            await DisplayAlert(AppText.Get("CheckoutZoneRequiredTitle"), AppText.Get("CheckoutZoneRequiredAlert"), AppText.Get("Ok"));
            return;
        }

        if (IsPrepaidSelected && string.IsNullOrWhiteSpace(PrepaidCardCode))
        {
            await DisplayAlert(AppText.Get("CheckoutCodeRequiredTitle"), AppText.Get("CheckoutCodeRequiredAlert"), AppText.Get("Ok"));
            return;
        }

        if (IsPrepaidSelected && !IsValidPrepaidInput(PrepaidCardCode))
        {
            await DisplayAlert(AppText.Get("CheckoutCardInvalidTitle"), AppText.Get("CheckoutCardInvalidAlert"), AppText.Get("Ok"));
            return;
        }

        IsBusy = true;
        _isSubmittingOrder = true;

        try
        {
            var line1 = string.IsNullOrWhiteSpace(SelectedAddress.District)
                ? SelectedAddress.Street
                : $"{SelectedAddress.Street}, {SelectedAddress.District}";

            var request = new CheckoutRequest(
                DeliveryName: SelectedAddress.FullName,
                DeliveryPhone: SelectedAddress.Phone,
                DeliveryLine1: line1,
                DeliveryCity: SelectedAddress.City,
                DeliveryCountry: SelectedAddress.Country,
                DeliveryZoneId: SelectedAddress.DeliveryZoneId,
                DeliveryNotes: SelectedAddress.Landmark,
                PromoCode: _promoCode,
                PaymentProvider: SelectedPaymentMethod,
                PrepaidCardCode: IsPrepaidSelected ? NormalizePrepaidPayload(PrepaidCardCode) : null);

            var order = await _ordersApi.CreateAsync(request, CancellationToken.None);

            try
            {
                await _cartApi.ClearAsync(CancellationToken.None);
            }
            catch
            {
                // La commande est creee, l'echec de nettoyage du panier ne bloque pas la suite.
            }

            string? paymentWarning = null;
            if (IsPayPalSelected)
            {
                paymentWarning = await HandlePayPalPaymentAsync(order.Id);
            }
            else if (IsPrepaidSelected)
            {
                paymentWarning = await HandlePrepaidPaymentAsync(order.Id);
            }

            try
            {
                order = await _ordersApi.GetByIdAsync(order.Id, CancellationToken.None);
            }
            catch
            {
                // On garde la commande locale si le refresh detail echoue.
            }

            if (IsPayPalSelected)
            {
                var paymentReturnPage = _services.GetRequiredService<PaymentReturnPage>();
                paymentReturnPage.Load(order.Id, "PayPal", paymentWarning);
                await NavigationHelper.PushAsync(paymentReturnPage);
            }
            else
            {
                var detailsPage = _services.GetRequiredService<OrderDetailsPage>();
                detailsPage.Load(order.Id, order);
                await NavigationHelper.PushAsync(detailsPage);

                if (!string.IsNullOrWhiteSpace(paymentWarning))
                {
                    await DisplayAlert(AppText.Get("PaymentTitle"), paymentWarning, AppText.Get("Ok"));
                }
            }
        }
        catch (ApiException ex)
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), ex.Message, AppText.Get("Ok"));
        }
        catch
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("CheckoutCreateFailed"), AppText.Get("Ok"));
        }
        finally
        {
            IsBusy = false;
            _isSubmittingOrder = false;
        }
    }

    private async Task<string?> HandlePayPalPaymentAsync(Guid orderId)
    {
        try
        {
            var checkout = await _paymentsApi.CreatePayPalCheckoutAsync(orderId, CancellationToken.None);
            var openNow = await DisplayAlert(
                AppText.Get("PayPalPaymentTitle"),
                AppText.Get("PayPalOpenNowPrompt"),
                AppText.Get("Yes"),
                AppText.Get("LaterAction"));

            if (openNow && Uri.TryCreate(checkout.Url, UriKind.Absolute, out var checkoutUri))
            {
                var canOpen = await Launcher.Default.CanOpenAsync(checkoutUri);
                if (!canOpen)
                {
                    return AppText.Get("PayPalOpenFailed");
                }

                try
                {
                    await Launcher.Default.OpenAsync(checkoutUri);
                }
                catch (InvalidOperationException)
                {
                    return AppText.Get("PayPalTemporaryUnavailable");
                }
            }
        }
        catch (ApiException ex)
        {
            return string.Format(AppText.Get("PayPalRedirectFailed"), ex.Message);
        }
        catch
        {
            return AppText.Get("PayPalOpenGenericFailed");
        }

        return null;
    }

    private async Task<string?> HandlePrepaidPaymentAsync(Guid orderId)
    {
        try
        {
            var result = await _paymentsApi.PayWithPrepaidCardAsync(orderId, NormalizePrepaidPayload(PrepaidCardCode), CancellationToken.None);
            await DisplayAlert(AppText.Get("PrepaidCardLabel"), result.Message, AppText.Get("Ok"));
        }
        catch (ApiException ex)
        {
            return string.Format(AppText.Get("PrepaidPaymentNotConfirmed"), ex.Message);
        }
        catch
        {
            return AppText.Get("PrepaidPaymentUnavailable");
        }

        return null;
    }

    private async void OnBackClicked(object sender, EventArgs e)
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
                await Shell.Current.GoToAsync("//orders");
            }
        }
        catch
        {
            await DisplayAlert(AppText.Get("NavigationTitle"), AppText.Get("NavigationBackFailed"), AppText.Get("Ok"));
        }
    }

    private static string NormalizePrepaidPayload(string? value)
    {
        var raw = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (raw.StartsWith("DM-", StringComparison.Ordinal))
        {
            return raw;
        }

        return GetCardDigits(raw);
    }

    private static bool IsValidPrepaidInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.StartsWith("DM-", StringComparison.Ordinal))
        {
            return normalized.Length >= 8;
        }

        var digits = GetCardDigits(normalized);
        return IsSupportedCardNumber(digits);
    }

    private static string GetCardDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static bool IsSupportedCardNumber(string digits)
    {
        if (digits.Length is < 13 or > 19)
        {
            return false;
        }

        if (!IsVisa(digits) && !IsMastercard(digits))
        {
            return false;
        }

        return IsLuhnValid(digits);
    }

    private static string DetermineCardBrand(string digits)
    {
        if (IsVisa(digits))
        {
            return "VISA";
        }

        if (IsMastercard(digits))
        {
            return "MASTERCARD";
        }

        return "UNKNOWN";
    }

    private static bool IsVisa(string digits)
        => !string.IsNullOrWhiteSpace(digits) && digits.StartsWith("4", StringComparison.Ordinal);

    private static bool IsMastercard(string digits)
    {
        if (string.IsNullOrWhiteSpace(digits))
        {
            return false;
        }

        if (digits.Length >= 2 &&
            int.TryParse(digits[..2], out var firstTwo) &&
            firstTwo is >= 51 and <= 55)
        {
            return true;
        }

        if (digits.Length >= 4 &&
            int.TryParse(digits[..4], out var firstFour) &&
            firstFour is >= 2221 and <= 2720)
        {
            return true;
        }

        return false;
    }

    private static bool IsLuhnValid(string digits)
    {
        var sum = 0;
        var shouldDouble = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(digits[i]))
            {
                return false;
            }

            var digit = digits[i] - '0';
            if (shouldDouble)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            shouldDouble = !shouldDouble;
        }

        return sum % 10 == 0;
    }

    private static bool IsStockIssue(ApiException ex)
        => ex.StatusCode == 409 ||
           string.Equals(ex.ErrorCode, "stock_insufficient", StringComparison.OrdinalIgnoreCase) ||
           ex.Message.Contains("Stock insuffisant", StringComparison.OrdinalIgnoreCase);
}
