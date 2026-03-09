using DoorMarket.Mobile.Localization;
using DoorMarket.Mobile.Services;
using DoorMarket.Mobile.Http;
using DoorMarket.Application.DTOs.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;
using System.Globalization;

namespace DoorMarket.Mobile.Pages;

public partial class PaymentReturnPage : LocalizedContentPage
{
    private readonly OrdersApiClient _ordersApi;
    private readonly PaymentsApiClient _paymentsApi;
    private readonly IServiceProvider _services;

    private Guid _orderId;
    private string _provider = "PayPal";
    private string _notice = string.Empty;
    private bool _isBusy;
    private bool _isPaid;
    private bool _isFailed;
    private bool _showProviderAction = true;
    private string _orderCode = string.Empty;
    private string _providerLabel = string.Empty;
    private string _lastCheckLabel = string.Empty;
    private string _statusTitle = string.Empty;
    private string _statusHint = string.Empty;
    private string _primaryActionLabel = string.Empty;
    private string _reopenProviderLabel = string.Empty;
    private Color _statusColor = Colors.Gray;
    private CancellationTokenSource? _pollCts;

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

    public string ProviderLabel
    {
        get => _providerLabel;
        set
        {
            if (_providerLabel == value)
            {
                return;
            }

            _providerLabel = value;
            OnPropertyChanged();
        }
    }

    public string LastCheckLabel
    {
        get => _lastCheckLabel;
        set
        {
            if (_lastCheckLabel == value)
            {
                return;
            }

            _lastCheckLabel = value;
            OnPropertyChanged();
        }
    }

    public string StatusTitle
    {
        get => _statusTitle;
        set
        {
            if (_statusTitle == value)
            {
                return;
            }

            _statusTitle = value;
            OnPropertyChanged();
        }
    }

    public string StatusHint
    {
        get => _statusHint;
        set
        {
            if (_statusHint == value)
            {
                return;
            }

            _statusHint = value;
            OnPropertyChanged();
        }
    }

    public string PrimaryActionLabel
    {
        get => _primaryActionLabel;
        set
        {
            if (_primaryActionLabel == value)
            {
                return;
            }

            _primaryActionLabel = value;
            OnPropertyChanged();
        }
    }

    public string ReopenProviderLabel
    {
        get => _reopenProviderLabel;
        set
        {
            if (_reopenProviderLabel == value)
            {
                return;
            }

            _reopenProviderLabel = value;
            OnPropertyChanged();
        }
    }

    public Color StatusColor
    {
        get => _statusColor;
        set
        {
            if (_statusColor == value)
            {
                return;
            }

            _statusColor = value;
            OnPropertyChanged();
        }
    }

    public bool ShowProviderAction
    {
        get => _showProviderAction;
        set
        {
            if (_showProviderAction == value)
            {
                return;
            }

            _showProviderAction = value;
            OnPropertyChanged();
        }
    }

    public string Notice
    {
        get => _notice;
        set
        {
            if (_notice == value)
            {
                return;
            }

            _notice = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasNotice));
        }
    }

    public bool HasNotice => !string.IsNullOrWhiteSpace(Notice);

    public PaymentReturnPage(OrdersApiClient ordersApi, PaymentsApiClient paymentsApi, IServiceProvider services)
    {
        InitializeComponent();
        _ordersApi = ordersApi;
        _paymentsApi = paymentsApi;
        _services = services;
        BindingContext = this;

        LastCheckLabel = AppText.Get("PaymentReturnChecking");
        PrimaryActionLabel = AppText.Get("PaymentReturnCheckNowAction");
    }

    public void Load(Guid orderId, string provider, string? notice = null)
    {
        _orderId = orderId;
        _provider = string.IsNullOrWhiteSpace(provider) ? "PayPal" : provider.Trim();
        Notice = notice ?? string.Empty;

        OrderCode = BuildOrderCode(orderId);
        ProviderLabel = string.Format(CultureInfo.CurrentCulture, AppText.Get("PaymentReturnProviderFormat"), _provider);
        ReopenProviderLabel = string.Format(CultureInfo.CurrentCulture, AppText.Get("PaymentReturnReopenProviderFormat"), _provider);
        ShowProviderAction = IsExternalProvider(_provider);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_orderId == Guid.Empty)
        {
            return;
        }

        await RefreshStatusAsync();
        StartPolling();
    }

    protected override void OnDisappearing()
    {
        StopPolling();
        base.OnDisappearing();
    }

    private async Task RefreshStatusAsync()
    {
        if (IsBusy || _orderId == Guid.Empty)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var order = await _ordersApi.GetByIdAsync(_orderId, CancellationToken.None);
            ApplyPaymentState(order);
            LastCheckLabel = string.Format(
                CultureInfo.CurrentCulture,
                AppText.Get("PaymentReturnLastCheckFormat"),
                DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
        }
        catch (ApiException ex)
        {
            StatusTitle = AppText.Get("PaymentReturnStatusErrorTitle");
            StatusHint = ex.Message;
            StatusColor = (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ErrorRed"];
        }
        catch
        {
            StatusTitle = AppText.Get("PaymentReturnStatusErrorTitle");
            StatusHint = AppText.Get("PaymentReturnStatusErrorHint");
            StatusColor = (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ErrorRed"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyPaymentState(OrderDto order)
    {
        var normalized = (order.PaymentStatus ?? string.Empty).Trim().ToLowerInvariant();
        var resources = Microsoft.Maui.Controls.Application.Current!.Resources;
        var success = (Color)resources["SuccessGreen"];
        var warning = (Color)resources["DoorOrange"];
        var error = (Color)resources["ErrorRed"];

        _isPaid = normalized == "paid";
        _isFailed = normalized == "failed";

        if (_isPaid)
        {
            StatusTitle = AppText.Get("PaymentReturnStatusPaidTitle");
            StatusHint = AppText.Get("PaymentReturnStatusPaidHint");
            StatusColor = success;
            PrimaryActionLabel = AppText.Get("PaymentReturnOpenOrderAction");
            ShowProviderAction = false;
            StopPolling();
            return;
        }

        if (_isFailed)
        {
            StatusTitle = AppText.Get("PaymentReturnStatusFailedTitle");
            StatusHint = AppText.Get("PaymentReturnStatusFailedHint");
            StatusColor = error;
        }
        else
        {
            StatusTitle = AppText.Get("PaymentReturnStatusPendingTitle");
            StatusHint = AppText.Get("PaymentReturnStatusPendingHint");
            StatusColor = warning;
        }

        PrimaryActionLabel = AppText.Get("PaymentReturnCheckNowAction");
        ShowProviderAction = IsExternalProvider(_provider);
    }

    private void StartPolling()
    {
        StopPolling();
        if (_orderId == Guid.Empty)
        {
            return;
        }

        _pollCts = new CancellationTokenSource();
        var token = _pollCts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (token.IsCancellationRequested || _isPaid || _isFailed)
                {
                    continue;
                }

                try
                {
                    await MainThread.InvokeOnMainThreadAsync(RefreshStatusAsync);
                }
                catch
                {
                    // Ignore transient polling errors.
                }
            }
        }, token);
    }

    private void StopPolling()
    {
        try
        {
            _pollCts?.Cancel();
        }
        catch
        {
            // Best effort.
        }
        finally
        {
            _pollCts?.Dispose();
            _pollCts = null;
        }
    }

    private async void OnPrimaryActionClicked(object sender, EventArgs e)
    {
        if (_isPaid)
        {
            await OpenOrderDetailsAsync();
            return;
        }

        await RefreshStatusAsync();
    }

    private async void OnReopenProviderClicked(object sender, EventArgs e)
    {
        if (!ShowProviderAction || _orderId == Guid.Empty || IsBusy)
        {
            return;
        }

        try
        {
            if (_provider.Contains("Stripe", StringComparison.OrdinalIgnoreCase))
            {
                var checkout = await _paymentsApi.CreateStripeCheckoutSessionAsync(_orderId, CancellationToken.None);
                await OpenExternalPaymentAsync(checkout.Url, AppText.Get("StripeOpenFailed"));
            }
            else
            {
                var checkout = await _paymentsApi.CreatePayPalCheckoutAsync(_orderId, CancellationToken.None);
                await OpenExternalPaymentAsync(checkout.Url, AppText.Get("PayPalOpenFailed"));
            }
        }
        catch (ApiException ex)
        {
            await DisplayAlert(AppText.Get("PaymentTitle"), ex.Message, AppText.Get("Ok"));
        }
        catch
        {
            await DisplayAlert(AppText.Get("PaymentTitle"), AppText.Get("PaymentActionUnavailable"), AppText.Get("Ok"));
        }
    }

    private async Task OpenExternalPaymentAsync(string url, string fallbackMessage)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            await DisplayAlert(AppText.Get("PaymentTitle"), fallbackMessage, AppText.Get("Ok"));
            return;
        }

        var canOpen = await Launcher.Default.CanOpenAsync(uri);
        if (!canOpen)
        {
            await DisplayAlert(AppText.Get("PaymentTitle"), fallbackMessage, AppText.Get("Ok"));
            return;
        }

        await Launcher.Default.OpenAsync(uri);
    }

    private async void OnOpenSupportClicked(object sender, EventArgs e)
    {
        if (Shell.Current is null)
        {
            await DisplayAlert(AppText.Get("NavigationTitle"), AppText.Get("NavigationOpenSupportFailed"), AppText.Get("Ok"));
            return;
        }

        var subject = string.Format(CultureInfo.CurrentCulture, AppText.Get("SupportPaymentSubjectFormat"), OrderCode);
        var message = string.Format(CultureInfo.CurrentCulture, AppText.Get("SupportPaymentMessageFormat"), OrderCode, StatusTitle, _provider);
        var route = $"{nameof(SupportPage)}?openForm=1&subject={Uri.EscapeDataString(subject)}&message={Uri.EscapeDataString(message)}";
        await Shell.Current.GoToAsync(route);
    }

    private async void OnOpenOrderDetailsClicked(object sender, EventArgs e)
    {
        await OpenOrderDetailsAsync();
    }

    private async Task OpenOrderDetailsAsync()
    {
        if (_orderId == Guid.Empty)
        {
            return;
        }

        var detailsPage = _services.GetRequiredService<OrderDetailsPage>();
        detailsPage.Load(_orderId);
        await NavigationHelper.PushAsync(detailsPage);
    }

    private async void OnBackToOrdersClicked(object sender, EventArgs e)
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("//orders");
            return;
        }

        await NavigationHelper.PopAsync();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await NavigationHelper.PopAsync();
    }

    private static bool IsExternalProvider(string provider)
        => provider.Contains("paypal", StringComparison.OrdinalIgnoreCase) ||
           provider.Contains("stripe", StringComparison.OrdinalIgnoreCase);

    private static string BuildOrderCode(Guid id)
    {
        var compact = id.ToString("N", CultureInfo.InvariantCulture).ToUpperInvariant();
        return $"DM{compact[..6]}";
    }
}
