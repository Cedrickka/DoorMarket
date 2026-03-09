using DoorMarket.Mobile.Localization;
using System.Collections.ObjectModel;
using System.Globalization;
using DoorMarket.Application.DTOs.Orders;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;

namespace DoorMarket.Mobile.Pages;

public partial class OrderDetailsPage : LocalizedContentPage
{
    private readonly OrdersApiClient _ordersApi;
    private readonly PaymentsApiClient _paymentsApi;
    private readonly IServiceProvider _services;
    private readonly Color _doorBlue;
    private readonly Color _softGray;
    private readonly Color _darkGray;

    private Guid _orderId;
    private OrderDto? _prefetchedOrder;

    private bool _isBusy;
    private string _currency = "USD";
    private decimal _subtotal;
    private decimal _delivery;
    private decimal _discount;
    private decimal _total;

    private string _orderCode = string.Empty;
    private string _createdAtLabel = string.Empty;
    private string _statusLabel = string.Empty;
    private string _paymentStatusLabel = string.Empty;
    private string _paymentProviderLabel = string.Empty;
    private string _statusDescription = string.Empty;
    private string _paymentPrepaidCode = string.Empty;
    private string _paymentActionHint = string.Empty;

    private Color _statusBackgroundColor = Colors.Gray;
    private Color _statusBorderColor = Colors.Gray;
    private Color _statusTextColor = Colors.White;
    private Color _paymentStatusColor = Colors.Gray;
    private Color _paymentStatusBackground = Colors.Transparent;
    private Color _paymentStatusBorder = Colors.Transparent;
    private Color _paymentActionHintColor = Colors.Gray;

    private bool _stageCreatedActive;
    private bool _stagePreparingActive;
    private bool _stageReadyActive;
    private bool _stageDeliveredActive;
    private bool _showPaymentActions;
    private bool _showProviderPaymentAction = true;
    private bool _isPaymentActionBusy;
    private string _primaryPaymentActionLabel = string.Empty;
    private string _paymentProviderKey = "paypal";
    private DateTime? _lastPaymentAutoRefreshUtc;
    private CancellationTokenSource? _paymentAutoRefreshCts;

    public ObservableCollection<OrderItemDto> Items { get; } = new();

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
            OnPropertyChanged(nameof(CanPayNow));
            OnPropertyChanged(nameof(CanPayNowWithPrepaid));
            OnPropertyChanged(nameof(CanRefreshPaymentStatus));
        }
    }

    public string OrderCode
    {
        get => _orderCode;
        set
        {
            if (_orderCode == value)
            {
                return;
            }

            _orderCode = value;
            OnPropertyChanged();
        }
    }

    public string CreatedAtLabel
    {
        get => _createdAtLabel;
        set
        {
            if (_createdAtLabel == value)
            {
                return;
            }

            _createdAtLabel = value;
            OnPropertyChanged();
        }
    }

    public string StatusLabel
    {
        get => _statusLabel;
        set
        {
            if (_statusLabel == value)
            {
                return;
            }

            _statusLabel = value;
            OnPropertyChanged();
        }
    }

    public Color StatusBackgroundColor
    {
        get => _statusBackgroundColor;
        set
        {
            if (_statusBackgroundColor == value)
            {
                return;
            }

            _statusBackgroundColor = value;
            OnPropertyChanged();
        }
    }

    public Color StatusBorderColor
    {
        get => _statusBorderColor;
        set
        {
            if (_statusBorderColor == value)
            {
                return;
            }

            _statusBorderColor = value;
            OnPropertyChanged();
        }
    }

    public Color StatusTextColor
    {
        get => _statusTextColor;
        set
        {
            if (_statusTextColor == value)
            {
                return;
            }

            _statusTextColor = value;
            OnPropertyChanged();
        }
    }

    public string PaymentStatusLabel
    {
        get => _paymentStatusLabel;
        set
        {
            if (_paymentStatusLabel == value)
            {
                return;
            }

            _paymentStatusLabel = value;
            OnPropertyChanged();
        }
    }

    public string PaymentProviderLabel
    {
        get => _paymentProviderLabel;
        set
        {
            if (_paymentProviderLabel == value)
            {
                return;
            }

            _paymentProviderLabel = value;
            OnPropertyChanged();
        }
    }

    public Color PaymentStatusColor
    {
        get => _paymentStatusColor;
        set
        {
            if (_paymentStatusColor == value)
            {
                return;
            }

            _paymentStatusColor = value;
            OnPropertyChanged();
        }
    }

    public Color PaymentStatusBackground
    {
        get => _paymentStatusBackground;
        set
        {
            if (_paymentStatusBackground == value)
            {
                return;
            }

            _paymentStatusBackground = value;
            OnPropertyChanged();
        }
    }

    public Color PaymentStatusBorder
    {
        get => _paymentStatusBorder;
        set
        {
            if (_paymentStatusBorder == value)
            {
                return;
            }

            _paymentStatusBorder = value;
            OnPropertyChanged();
        }
    }

    public string StatusDescription
    {
        get => _statusDescription;
        set
        {
            if (_statusDescription == value)
            {
                return;
            }

            _statusDescription = value;
            OnPropertyChanged();
        }
    }

    public string ItemsCountLabel => Items.Count == 1
        ? AppText.Get("OrderItemsSingle")
        : string.Format(CultureInfo.CurrentCulture, AppText.Get("OrderItemsPluralFormat"), Items.Count);

    public Color CreatedStageBackground => GetStageBackground(_stageCreatedActive);

    public Color CreatedStageTextColor => GetStageTextColor(_stageCreatedActive);

    public Color PreparingStageBackground => GetStageBackground(_stagePreparingActive);

    public Color PreparingStageTextColor => GetStageTextColor(_stagePreparingActive);

    public Color ReadyStageBackground => GetStageBackground(_stageReadyActive);

    public Color ReadyStageTextColor => GetStageTextColor(_stageReadyActive);

    public Color DeliveredStageBackground => GetStageBackground(_stageDeliveredActive);

    public Color DeliveredStageTextColor => GetStageTextColor(_stageDeliveredActive);

    public string SubtotalText => FormatMoney(_subtotal);

    public string DeliveryText => FormatMoney(_delivery);

    public string DiscountText => _discount <= 0m
        ? FormatMoney(0m)
        : $"-{FormatMoney(_discount)}";

    public string TotalText => FormatMoney(_total);

    public bool ShowPaymentActions
    {
        get => _showPaymentActions;
        set
        {
            if (_showPaymentActions == value)
            {
                return;
            }

            _showPaymentActions = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanPayNow));
            OnPropertyChanged(nameof(CanPayNowWithPrepaid));
            OnPropertyChanged(nameof(CanRefreshPaymentStatus));
            OnPropertyChanged(nameof(ShowPaymentAutoRefreshHint));
            OnPropertyChanged(nameof(PaymentAutoRefreshHint));
        }
    }

    public bool ShowProviderPaymentAction
    {
        get => _showProviderPaymentAction;
        set
        {
            if (_showProviderPaymentAction == value)
            {
                return;
            }

            _showProviderPaymentAction = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanPayNow));
        }
    }

    public string PrimaryPaymentActionLabel
    {
        get => _primaryPaymentActionLabel;
        set
        {
            if (_primaryPaymentActionLabel == value)
            {
                return;
            }

            _primaryPaymentActionLabel = value;
            OnPropertyChanged();
        }
    }

    public string PaymentPrepaidCode
    {
        get => _paymentPrepaidCode;
        set
        {
            if (_paymentPrepaidCode == value)
            {
                return;
            }

            _paymentPrepaidCode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanPayNowWithPrepaid));
        }
    }

    public bool CanPayNow => !IsBusy && !_isPaymentActionBusy && ShowPaymentActions && ShowProviderPaymentAction;

    public bool CanPayNowWithPrepaid => !IsBusy && !_isPaymentActionBusy && ShowPaymentActions && IsValidPrepaidInput(PaymentPrepaidCode);

    public bool CanRefreshPaymentStatus => !IsBusy && !_isPaymentActionBusy && ShowPaymentActions;

    public bool ShowPaymentAutoRefreshHint => ShowPaymentActions;

    public string PaymentAutoRefreshHint
    {
        get
        {
            if (_lastPaymentAutoRefreshUtc is null)
            {
                return AppText.Get("PaymentAutoRefreshHint");
            }

            var local = _lastPaymentAutoRefreshUtc.Value.ToLocalTime()
                .ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            return string.Format(
                CultureInfo.CurrentCulture,
                AppText.Get("PaymentAutoRefreshWithTimeHint"),
                local);
        }
    }

    public string PaymentActionHint
    {
        get => _paymentActionHint;
        set
        {
            if (_paymentActionHint == value)
            {
                return;
            }

            _paymentActionHint = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPaymentActionHint));
        }
    }

    public bool HasPaymentActionHint => !string.IsNullOrWhiteSpace(PaymentActionHint);

    public Color PaymentActionHintColor
    {
        get => _paymentActionHintColor;
        set
        {
            if (_paymentActionHintColor == value)
            {
                return;
            }

            _paymentActionHintColor = value;
            OnPropertyChanged();
        }
    }

    public OrderDetailsPage(OrdersApiClient ordersApi, PaymentsApiClient paymentsApi, IServiceProvider services)
    {
        InitializeComponent();
        _ordersApi = ordersApi;
        _paymentsApi = paymentsApi;
        _services = services;

        var resources = Microsoft.Maui.Controls.Application.Current!.Resources;
        _doorBlue = (Color)resources["DoorBlue"];
        _softGray = (Color)resources["SoftGray"];
        _darkGray = (Color)resources["DarkGray"];

        BindingContext = this;
    }

    public void Load(Guid orderId, OrderDto? prefetchedOrder = null)
    {
        _orderId = orderId;
        _prefetchedOrder = prefetchedOrder;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_orderId == Guid.Empty)
        {
            return;
        }

        await LoadOrderAsync();
        StartPaymentAutoRefreshLoop();
    }

    protected override void OnDisappearing()
    {
        StopPaymentAutoRefreshLoop();
        base.OnDisappearing();
    }

    protected override async Task RefreshLanguageDataAsync()
    {
        if (_orderId == Guid.Empty || IsBusy)
        {
            return;
        }

        await LoadOrderAsync();
    }

    private async Task LoadOrderAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var order = _prefetchedOrder ?? await _ordersApi.GetByIdAsync(_orderId, CancellationToken.None);
            _prefetchedOrder = null;
            ApplyOrder(order);
            _lastPaymentAutoRefreshUtc = DateTime.UtcNow;
            OnPropertyChanged(nameof(PaymentAutoRefreshHint));
        }
        catch (ApiException ex)
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), ex.Message, AppText.Get("Ok"));
        }
        catch
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("OrderDetailsLoadFailed"), AppText.Get("Ok"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyOrder(OrderDto order)
    {
        _currency = string.IsNullOrWhiteSpace(order.Currency) ? "USD" : order.Currency;
        _subtotal = order.Subtotal;
        _delivery = order.DeliveryFee;
        _discount = order.Discount;
        _total = order.TotalAmount;

        OrderCode = BuildOrderCode(order.Id);
        CreatedAtLabel = order.CreatedAtUtc.ToLocalTime().ToString("dd MMM yyyy - HH:mm", CultureInfo.CurrentCulture);

        var (label, backgroundColor, borderColor, textColor, description) = MapStatus(order.Status, order.PaymentStatus);
        StatusLabel = label;
        StatusBackgroundColor = backgroundColor;
        StatusBorderColor = borderColor;
        StatusTextColor = textColor;
        StatusDescription = description;

        var (paymentLabel, paymentColor) = MapPaymentStatus(order.PaymentStatus);
        PaymentStatusLabel = paymentLabel;
        PaymentStatusColor = paymentColor;
        PaymentStatusBackground = paymentColor.WithAlpha(0.18f);
        PaymentStatusBorder = paymentColor.WithAlpha(0.45f);
        PaymentProviderLabel = string.IsNullOrWhiteSpace(order.PaymentProvider) ? AppText.Get("NotAvailableShort") : order.PaymentProvider;
        _paymentProviderKey = ResolvePaymentProvider(order);
        ShowProviderPaymentAction = true;
        PrimaryPaymentActionLabel = _paymentProviderKey == "stripe" ? AppText.Get("StripeLabel") : AppText.Get("PayPalLabel");
        ShowPaymentActions = !string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase);
        _isPaymentActionBusy = false;
        PaymentPrepaidCode = string.Empty;
        var paymentNormalized = (order.PaymentStatus ?? string.Empty).Trim().ToLowerInvariant();
        if (!ShowPaymentActions)
        {
            PaymentActionHint = AppText.Get("PaymentConfirmedHint");
            PaymentActionHintColor = (Color)Microsoft.Maui.Controls.Application.Current!.Resources["SuccessGreen"];
        }
        else if (paymentNormalized == "pending")
        {
            PaymentActionHint = AppText.Get("PaymentPendingRecoveryHint");
            PaymentActionHintColor = (Color)Microsoft.Maui.Controls.Application.Current!.Resources["DoorOrange"];
        }
        else if (paymentNormalized == "failed")
        {
            PaymentActionHint = AppText.Get("PaymentFailedRecoveryHint");
            PaymentActionHintColor = (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ErrorRed"];
        }
        else
        {
            PaymentActionHint = AppText.Get("PaymentRequiredHint");
            PaymentActionHintColor = (Color)Microsoft.Maui.Controls.Application.Current!.Resources["DoorBlue"];
        }
        OnPropertyChanged(nameof(CanPayNow));
        OnPropertyChanged(nameof(CanPayNowWithPrepaid));
        OnPropertyChanged(nameof(CanRefreshPaymentStatus));

        ApplyProgressState(order.Status ?? string.Empty, order.PaymentStatus ?? string.Empty);

        Items.Clear();
        foreach (var item in order.Items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(nameof(ItemsCountLabel));
        OnPropertyChanged(nameof(SubtotalText));
        OnPropertyChanged(nameof(DeliveryText));
        OnPropertyChanged(nameof(DiscountText));
        OnPropertyChanged(nameof(TotalText));
    }

    private void ApplyProgressState(string status, string paymentStatus)
    {
        var normalizedStatus = (status ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedPayment = (paymentStatus ?? string.Empty).Trim().ToLowerInvariant();

        _stageCreatedActive = true;

        var progressed = normalizedPayment == "paid" ||
                         normalizedStatus is "preparing" or "ready" or "outfordelivery" or "delivered" or "completed";

        _stagePreparingActive = progressed;
        _stageReadyActive = normalizedStatus is "ready" or "outfordelivery" or "delivered" or "completed";
        _stageDeliveredActive = normalizedStatus is "delivered" or "completed";

        OnPropertyChanged(nameof(CreatedStageBackground));
        OnPropertyChanged(nameof(CreatedStageTextColor));
        OnPropertyChanged(nameof(PreparingStageBackground));
        OnPropertyChanged(nameof(PreparingStageTextColor));
        OnPropertyChanged(nameof(ReadyStageBackground));
        OnPropertyChanged(nameof(ReadyStageTextColor));
        OnPropertyChanged(nameof(DeliveredStageBackground));
        OnPropertyChanged(nameof(DeliveredStageTextColor));
    }

    private Color GetStageBackground(bool isActive)
        => isActive ? _doorBlue : _softGray;

    private Color GetStageTextColor(bool isActive)
        => isActive ? Colors.White : _darkGray;

    private static string BuildOrderCode(Guid id)
    {
        var compact = id.ToString("N", CultureInfo.InvariantCulture).ToUpperInvariant();
        return $"DM{compact[..6]}";
    }

    private static (string Label, Color Color) MapPaymentStatus(string paymentStatus)
    {
        var resources = Microsoft.Maui.Controls.Application.Current!.Resources;
        var success = (Color)resources["SuccessGreen"];
        var warning = (Color)resources["WarningYellow"];
        var doorBlue = (Color)resources["DoorBlue"];
        var error = (Color)resources["ErrorRed"];
        var accent = (Color)resources["DoorOrange"];

        var normalized = (paymentStatus ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "paid" => (AppText.Get("PaymentStatusPaid"), success),
            "unpaid" => (AppText.Get("PaymentStatusUnpaid"), doorBlue),
            "pending" => (AppText.Get("PaymentStatusPending"), accent),
            "failed" => (AppText.Get("PaymentStatusFailed"), error),
            _ => (paymentStatus ?? string.Empty, warning)
        };
    }

    private static (string Label, Color BackgroundColor, Color BorderColor, Color TextColor, string Description) MapStatus(string status, string paymentStatus)
    {
        var resources = Microsoft.Maui.Controls.Application.Current!.Resources;
        var success = (Color)resources["SuccessGreen"];
        var warning = (Color)resources["WarningYellow"];
        var error = (Color)resources["ErrorRed"];
        var accent = (Color)resources["DoorOrange"];
        var doorBlue = (Color)resources["DoorBlue"];

        var statusNormalized = (status ?? string.Empty).Trim().ToLowerInvariant();
        var paymentNormalized = (paymentStatus ?? string.Empty).Trim().ToLowerInvariant();

        if (statusNormalized is "delivered" or "completed")
        {
            return (AppText.Get("StatusDelivered"), success.WithAlpha(0.18f), success.WithAlpha(0.45f), success, AppText.Get("OrderSubtitleCompleted"));
        }

        if (statusNormalized is "cancelled" or "failed")
        {
            return (AppText.Get("StatusFailed"), error.WithAlpha(0.16f), error.WithAlpha(0.42f), error, AppText.Get("OrderSubtitleCancelled"));
        }

        if (paymentNormalized != "paid")
        {
            return (AppText.Get("StatusPending"), warning.WithAlpha(0.28f), warning.WithAlpha(0.55f), doorBlue, AppText.Get("OrderSubtitlePaymentPending"));
        }

        return statusNormalized switch
        {
            "ready" => (AppText.Get("StatusReady"), accent.WithAlpha(0.16f), accent.WithAlpha(0.45f), accent, AppText.Get("OrderSubtitleReady")),
            "preparing" => (AppText.Get("StatusPreparing"), accent.WithAlpha(0.14f), accent.WithAlpha(0.40f), accent, AppText.Get("OrderSubtitlePreparing")),
            "outfordelivery" => (AppText.Get("StatusOutForDelivery"), accent.WithAlpha(0.20f), accent.WithAlpha(0.55f), accent, AppText.Get("OrderSubtitleOutForDelivery")),
            _ => (AppText.Get("StatusOngoing"), warning.WithAlpha(0.28f), warning.WithAlpha(0.55f), doorBlue, AppText.Get("OrderSubtitleProcessing"))
        };
    }

    private string FormatMoney(decimal value)
        => string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", value, _currency);

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
            await DisplayAlert(AppText.Get("NavigationTitle"), AppText.Get("OrdersBackFailed"), AppText.Get("Ok"));
        }
    }

    private async void OnPayNowWithPayPalClicked(object sender, EventArgs e)
    {
        if (!CanPayNow)
        {
            return;
        }

        await RunPaymentActionAsync(async () =>
        {
            if (_paymentProviderKey == "stripe")
            {
                var checkout = await _paymentsApi.CreateStripeCheckoutSessionAsync(_orderId, CancellationToken.None);
                var openNow = await DisplayAlert(
                    AppText.Get("StripeLabel"),
                    AppText.Get("StripeOpenNowPrompt"),
                    AppText.Get("Yes"),
                    AppText.Get("LaterAction"));

                if (openNow && Uri.TryCreate(checkout.Url, UriKind.Absolute, out var checkoutUri))
                {
                    var canOpen = await Launcher.Default.CanOpenAsync(checkoutUri);
                    if (!canOpen)
                    {
                        SetPaymentActionHint(AppText.Get("StripeOpenFailed"), (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ErrorRed"]);
                        return;
                    }

                    try
                    {
                        await Launcher.Default.OpenAsync(checkoutUri);
                    }
                    catch (InvalidOperationException)
                    {
                        SetPaymentActionHint(AppText.Get("StripeTemporaryUnavailable"), (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ErrorRed"]);
                        return;
                    }
                }

                SetPaymentActionHint(AppText.Get("StripeRedirectReady"), (Color)Microsoft.Maui.Controls.Application.Current!.Resources["SuccessGreen"]);
                var returnPage = _services.GetRequiredService<PaymentReturnPage>();
                returnPage.Load(_orderId, "Stripe");
                await NavigationHelper.PushAsync(returnPage);
                return;
            }

            var payPalCheckout = await _paymentsApi.CreatePayPalCheckoutAsync(_orderId, CancellationToken.None);
            var payPalOpenNow = await DisplayAlert(
                AppText.Get("PayPalLabel"),
                AppText.Get("PayPalOpenNowPrompt"),
                AppText.Get("Yes"),
                AppText.Get("LaterAction"));

            if (payPalOpenNow && Uri.TryCreate(payPalCheckout.Url, UriKind.Absolute, out var payPalUri))
            {
                var canOpen = await Launcher.Default.CanOpenAsync(payPalUri);
                if (!canOpen)
                {
                    SetPaymentActionHint(AppText.Get("PayPalOpenFailed"), (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ErrorRed"]);
                    return;
                }

                try
                {
                    await Launcher.Default.OpenAsync(payPalUri);
                }
                catch (InvalidOperationException)
                {
                    SetPaymentActionHint(AppText.Get("PayPalTemporaryUnavailable"), (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ErrorRed"]);
                    return;
                }
            }

            SetPaymentActionHint(AppText.Get("PayPalRedirectReady"), (Color)Microsoft.Maui.Controls.Application.Current!.Resources["SuccessGreen"]);
            var payPalReturnPage = _services.GetRequiredService<PaymentReturnPage>();
            payPalReturnPage.Load(_orderId, "PayPal");
            await NavigationHelper.PushAsync(payPalReturnPage);
        });
    }

    private async void OnPayNowWithPrepaidClicked(object sender, EventArgs e)
    {
        if (!CanPayNowWithPrepaid)
        {
            SetPaymentActionHint(AppText.Get("PrepaidCodeInvalidHint"), (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ErrorRed"]);
            return;
        }

        await RunPaymentActionAsync(async () =>
        {
            var result = await _paymentsApi.PayWithPrepaidCardAsync(_orderId, NormalizePrepaidPayload(PaymentPrepaidCode), CancellationToken.None);
            SetPaymentActionHint(result.Message, (Color)Microsoft.Maui.Controls.Application.Current!.Resources["SuccessGreen"]);
            await LoadOrderAsync();
        });
    }

    private async Task RunPaymentActionAsync(Func<Task> action)
    {
        if (_isPaymentActionBusy)
        {
            return;
        }

        _isPaymentActionBusy = true;
        OnPropertyChanged(nameof(CanPayNow));
        OnPropertyChanged(nameof(CanPayNowWithPrepaid));
        OnPropertyChanged(nameof(CanRefreshPaymentStatus));

        try
        {
            await action();
        }
        catch (ApiException ex)
        {
            SetPaymentActionHint(ex.Message, (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ErrorRed"]);
        }
        catch
        {
            SetPaymentActionHint(AppText.Get("PaymentActionUnavailable"), (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ErrorRed"]);
        }
        finally
        {
            _isPaymentActionBusy = false;
            OnPropertyChanged(nameof(CanPayNow));
            OnPropertyChanged(nameof(CanPayNowWithPrepaid));
            OnPropertyChanged(nameof(CanRefreshPaymentStatus));
        }
    }

    private async void OnRefreshPaymentStatusClicked(object sender, EventArgs e)
    {
        if (!CanRefreshPaymentStatus)
        {
            return;
        }

        await RunPaymentActionAsync(async () =>
        {
            await LoadOrderAsync();
            SetPaymentActionHint(AppText.Get("OrderStatusRefreshedHint"), (Color)Microsoft.Maui.Controls.Application.Current!.Resources["SuccessGreen"]);
        });
    }

    private async void OnOpenSupportClicked(object sender, EventArgs e)
    {
        try
        {
            if (Shell.Current is not null)
            {
                var orderCode = string.IsNullOrWhiteSpace(OrderCode) ? BuildOrderCode(_orderId) : OrderCode;
                var paymentStatus = string.IsNullOrWhiteSpace(PaymentStatusLabel) ? AppText.Get("NotAvailableShort") : PaymentStatusLabel;
                var provider = string.IsNullOrWhiteSpace(PaymentProviderLabel) ? AppText.Get("NotAvailableShort") : PaymentProviderLabel;
                var subject = string.Format(CultureInfo.CurrentCulture, AppText.Get("SupportPaymentSubjectFormat"), orderCode);
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    AppText.Get("SupportPaymentMessageFormat"),
                    orderCode,
                    paymentStatus,
                    provider);
                var route = $"{nameof(SupportPage)}?openForm=1&subject={Uri.EscapeDataString(subject)}&message={Uri.EscapeDataString(message)}";
                await Shell.Current.GoToAsync(route);
                return;
            }

            SetPaymentActionHint(
                AppText.Get("NavigationOpenSupportFailed"),
                (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ErrorRed"]);
        }
        catch
        {
            SetPaymentActionHint(
                AppText.Get("NavigationOpenSupportFailed"),
                (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ErrorRed"]);
        }
    }

    private void StartPaymentAutoRefreshLoop()
    {
        StopPaymentAutoRefreshLoop();
        _paymentAutoRefreshCts = new CancellationTokenSource();
        var token = _paymentAutoRefreshCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(12), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (!ShowPaymentActions || IsBusy || _isPaymentActionBusy)
                {
                    continue;
                }

                try
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        if (!ShowPaymentActions || IsBusy || _isPaymentActionBusy)
                        {
                            return;
                        }

                        await LoadOrderAsync();
                    });
                }
                catch
                {
                    // Ignore transient failures during passive status polling.
                }
            }
        }, token);
    }

    private void StopPaymentAutoRefreshLoop()
    {
        try
        {
            _paymentAutoRefreshCts?.Cancel();
        }
        catch
        {
            // Best effort cancellation.
        }
        finally
        {
            _paymentAutoRefreshCts?.Dispose();
            _paymentAutoRefreshCts = null;
        }
    }

    private void SetPaymentActionHint(string message, Color color)
    {
        PaymentActionHint = message;
        PaymentActionHintColor = color;
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

    private static string ResolvePaymentProvider(OrderDto order)
    {
        var raw = !string.IsNullOrWhiteSpace(order.PaymentProvider)
            ? order.PaymentProvider
            : order.PaymentMethodSnapshot?.Provider ?? string.Empty;
        var normalized = raw.Trim().ToLowerInvariant().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (normalized.Contains("stripe", StringComparison.Ordinal))
        {
            return "stripe";
        }

        if (normalized.Contains("prepaid", StringComparison.Ordinal) ||
            normalized.Contains("voucher", StringComparison.Ordinal))
        {
            return "prepaid";
        }

        return "paypal";
    }
}
