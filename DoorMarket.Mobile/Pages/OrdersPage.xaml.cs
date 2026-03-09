using DoorMarket.Mobile.Localization;
using System.Collections.ObjectModel;
using System.Globalization;
using DoorMarket.Application.DTOs.Orders;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Models;
using DoorMarket.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;

namespace DoorMarket.Mobile.Pages;

public partial class OrdersPage : LocalizedContentPage
{
    private readonly OrdersApiClient _ordersApi;
    private readonly IServiceProvider _services;
    private readonly Color _tabActiveColor;
    private readonly Color _tabInactiveColor;
    private readonly Color _tabActiveTextColor = Colors.White;
    private readonly Color _tabInactiveTextColor;
    private readonly Color _tabBorderColor;
    private bool _isNavigating;
    private Guid? _firstPendingOrderId;
    private int _pendingPaymentCount;
    private int _activeTab;

    private bool _isBusy;

    public ObservableCollection<OrderListItemViewModel> OngoingOrders { get; } = new();
    public ObservableCollection<OrderListItemViewModel> CompletedOrders { get; } = new();
    public ObservableCollection<OrderListItemViewModel> PendingOrders { get; } = new();

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
        }
    }

    public string OngoingCountLabel => OngoingOrders.Count.ToString(CultureInfo.InvariantCulture);

    public string CompletedCountLabel => CompletedOrders.Count.ToString(CultureInfo.InvariantCulture);

    public string OngoingTabTitle => string.Format(AppText.Get("OrdersOngoingWithCount"), OngoingOrders.Count);

    public string CompletedTabTitle => string.Format(AppText.Get("OrdersCompletedWithCount"), CompletedOrders.Count);

    public string PendingTabTitle => string.Format(AppText.Get("OrdersPendingPaymentsTitle"), PendingOrders.Count);

    public bool HasPendingPayments => _pendingPaymentCount > 0;

    public string PendingPaymentsTitle => string.Format(AppText.Get("OrdersPendingPaymentsTitle"), _pendingPaymentCount);

    public string PendingPaymentsHint => AppText.Get("OrdersPendingPaymentsHint");

    public OrdersPage(OrdersApiClient ordersApi, IServiceProvider services)
    {
        InitializeComponent();
        _ordersApi = ordersApi;
        _services = services;

        _tabActiveColor = ResolveColor("DoorOrange", Colors.Orange);
        _tabInactiveColor = ResolveColor("SoftGray", Colors.LightGray);
        _tabInactiveTextColor = ResolveColor("DoorBlue", Colors.DarkBlue);
        _tabBorderColor = ResolveColor("DoorBlue10", Colors.LightBlue);

        BindingContext = this;
        SetTab(0);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
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
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            // Keep a small page on mobile to avoid heavy allocations on low-memory devices.
            var result = await _ordersApi.GetMineAsync(page: 1, pageSize: 10, CancellationToken.None);
            BindOrders(result.Items);
        }
        catch (ApiException ex)
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), ex.Message, AppText.Get("Ok"));
        }
        catch
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("OrdersLoadFailed"), AppText.Get("Ok"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BindOrders(IReadOnlyList<OrderDto> orders)
    {
        OngoingOrders.Clear();
        CompletedOrders.Clear();
        PendingOrders.Clear();
        _firstPendingOrderId = null;
        _pendingPaymentCount = 0;

        foreach (var order in orders)
        {
            var viewModel = ToViewModel(order);
            if (viewModel.NeedsPaymentAction)
            {
                _pendingPaymentCount++;
                _firstPendingOrderId ??= viewModel.Id;
                PendingOrders.Add(viewModel);
            }

            if (IsCompletedOrder(order))
            {
                CompletedOrders.Add(viewModel);
            }
            else
            {
                OngoingOrders.Add(viewModel);
            }
        }

        OnPropertyChanged(nameof(HasPendingPayments));
        OnPropertyChanged(nameof(PendingPaymentsTitle));
        OnPropertyChanged(nameof(PendingPaymentsHint));
        OnPropertyChanged(nameof(PendingTabTitle));
        SetTab(_activeTab);
    }

    private static bool IsCompletedOrder(OrderDto order)
    {
        var status = string.IsNullOrWhiteSpace(order.FulfillmentStatus) ? order.Status : order.FulfillmentStatus;
        var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "delivered" or "completed" or "cancelled" or "failed";
    }

    private static OrderListItemViewModel ToViewModel(OrderDto order)
    {
        var (statusLabel, backgroundColor, borderColor, textColor, subtitle) = MapStatus(order.Status, order.PaymentStatus);

        return new OrderListItemViewModel
        {
            Id = order.Id,
            Code = BuildOrderCode(order.Id),
            DateLabel = order.CreatedAtUtc.ToLocalTime().ToString("dd MMM yyyy - HH:mm", CultureInfo.CurrentCulture),
            StatusLabel = statusLabel,
            StatusBackgroundColor = backgroundColor,
            StatusBorderColor = borderColor,
            StatusTextColor = textColor,
            StatusSubtitle = subtitle,
            PaymentProviderLabel = BuildPaymentProviderLabel(order.PaymentProvider, order.PaymentStatus),
            NeedsPaymentAction = NeedsPaymentAction(order.PaymentStatus),
            TotalLabel = string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", order.TotalAmount, order.Currency)
        };
    }

    private static bool NeedsPaymentAction(string paymentStatus)
    {
        var normalized = (paymentStatus ?? string.Empty).Trim().ToLowerInvariant();
        return !string.IsNullOrWhiteSpace(normalized) && normalized != "paid";
    }

    private static string BuildOrderCode(Guid id)
    {
        var compact = id.ToString("N", CultureInfo.InvariantCulture).ToUpperInvariant();
        return $"DM{compact[..6]}";
    }

    private static (string Label, Color BackgroundColor, Color BorderColor, Color TextColor, string Subtitle) MapStatus(string status, string paymentStatus)
    {
        var success = ResolveColor("SuccessGreen", Colors.Green);
        var warning = ResolveColor("WarningYellow", Colors.Gold);
        var error = ResolveColor("ErrorRed", Colors.Red);
        var accent = ResolveColor("DoorOrange", Colors.Orange);
        var doorBlue = ResolveColor("DoorBlue", Colors.DarkBlue);

        var statusNormalized = (status ?? string.Empty).Trim().ToLowerInvariant();
        var paymentNormalized = (paymentStatus ?? string.Empty).Trim().ToLowerInvariant();

        if (statusNormalized is "delivered" or "completed")
        {
            return (AppText.Get("StatusDelivered"), success.WithAlpha(0.18f), success.WithAlpha(0.5f), success, AppText.Get("OrderSubtitleCompleted"));
        }

        if (statusNormalized is "cancelled" or "failed")
        {
            return (AppText.Get("StatusFailed"), error.WithAlpha(0.16f), error.WithAlpha(0.45f), error, AppText.Get("OrderSubtitleCancelled"));
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

    private static string BuildPaymentProviderLabel(string paymentProvider, string paymentStatus)
    {
        if (string.IsNullOrWhiteSpace(paymentProvider))
        {
            return string.Empty;
        }

        var provider = paymentProvider.Trim();
        var status = string.IsNullOrWhiteSpace(paymentStatus) ? string.Empty : paymentStatus.Trim();
        return string.IsNullOrWhiteSpace(status)
            ? provider
            : $"{provider} - {status}";
    }

    private void SetTab(int tabIndex)
    {
        _activeTab = tabIndex;
        OngoingOrdersList.IsVisible = tabIndex == 0;
        CompletedOrdersList.IsVisible = tabIndex == 1;
        PendingOrdersList.IsVisible = tabIndex == 2;

        OnPropertyChanged(nameof(OngoingCountLabel));
        OnPropertyChanged(nameof(CompletedCountLabel));
        OnPropertyChanged(nameof(OngoingTabTitle));
        OnPropertyChanged(nameof(CompletedTabTitle));

        OngoingTabButton.BackgroundColor = tabIndex == 0 ? _tabActiveColor : _tabInactiveColor;
        OngoingTabButton.TextColor = tabIndex == 0 ? _tabActiveTextColor : _tabInactiveTextColor;
        OngoingTabButton.BorderColor = tabIndex == 0 ? _tabActiveColor : _tabBorderColor;
        OngoingTabButton.BorderWidth = tabIndex == 0 ? 0 : 1;

        CompletedTabButton.BackgroundColor = tabIndex == 1 ? _tabActiveColor : _tabInactiveColor;
        CompletedTabButton.TextColor = tabIndex == 1 ? _tabActiveTextColor : _tabInactiveTextColor;
        CompletedTabButton.BorderColor = tabIndex == 1 ? _tabActiveColor : _tabBorderColor;
        CompletedTabButton.BorderWidth = tabIndex == 1 ? 0 : 1;

        PendingTabButton.BackgroundColor = tabIndex == 2 ? _tabActiveColor : _tabInactiveColor;
        PendingTabButton.TextColor = tabIndex == 2 ? _tabActiveTextColor : _tabInactiveTextColor;
        PendingTabButton.BorderColor = tabIndex == 2 ? _tabActiveColor : _tabBorderColor;
        PendingTabButton.BorderWidth = tabIndex == 2 ? 0 : 1;
    }

    private void OnOngoingTabClicked(object sender, EventArgs e)
    {
        SetTab(0);
    }

    private void OnCompletedTabClicked(object sender, EventArgs e)
    {
        SetTab(1);
    }

    private void OnPendingTabClicked(object sender, EventArgs e)
    {
        SetTab(2);
    }

    private async void OnResumePendingPaymentClicked(object sender, EventArgs e)
    {
        if (_isNavigating || IsBusy || _firstPendingOrderId is null)
        {
            return;
        }

        _isNavigating = true;
        try
        {
            var detailsPage = _services.GetRequiredService<OrderDetailsPage>();
            detailsPage.Load(_firstPendingOrderId.Value);
            await NavigationHelper.PushAsync(detailsPage);
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppText.Get("NavigationTitle"), string.Format(AppText.Get("OrderOpenFailed"), ex.Message), AppText.Get("Ok"));
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private async void OnOrderSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is CollectionView collectionView)
        {
            collectionView.SelectedItem = null;
        }

        if (_isNavigating || IsBusy || e.CurrentSelection.FirstOrDefault() is not OrderListItemViewModel order)
        {
            return;
        }

        _isNavigating = true;
        try
        {
            var detailsPage = _services.GetRequiredService<OrderDetailsPage>();
            detailsPage.Load(order.Id);
            await NavigationHelper.PushAsync(detailsPage);
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppText.Get("NavigationTitle"), string.Format(AppText.Get("OrderOpenFailed"), ex.Message), AppText.Get("Ok"));
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private static Color ResolveColor(string resourceKey, Color fallback)
    {
        var resources = Microsoft.Maui.Controls.Application.Current?.Resources;
        if (resources is not null && resources.TryGetValue(resourceKey, out var value) && value is Color color)
        {
            return color;
        }

        return fallback;
    }
}
