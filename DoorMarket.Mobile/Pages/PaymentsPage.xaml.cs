using DoorMarket.Mobile.Localization;
using DoorMarket.Mobile.Services;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace DoorMarket.Mobile.Pages;

public partial class PaymentsPage : LocalizedContentPage
{
    private const string PreferredPaymentKey = "dm_preferred_payment";

    private string _selectedMethod = "PayPal";
    private string _prepaidCode = string.Empty;

    public string PrepaidCode
    {
        get => _prepaidCode;
        set
        {
            if (_prepaidCode == value)
            {
                return;
            }

            _prepaidCode = value;
            OnPropertyChanged();
        }
    }

    public Color PayPalCardBackground => IsPayPalSelected
        ? (Color)Microsoft.Maui.Controls.Application.Current!.Resources["DoorBlue"]
        : (Color)Microsoft.Maui.Controls.Application.Current!.Resources["White"];

    public Color PayPalCardBorder => IsPayPalSelected
        ? (Color)Microsoft.Maui.Controls.Application.Current!.Resources["DoorBlue"]
        : (Color)Microsoft.Maui.Controls.Application.Current!.Resources["DoorBlue10"];

    public Color PayPalCardText => IsPayPalSelected
        ? Colors.White
        : (Color)Microsoft.Maui.Controls.Application.Current!.Resources["DoorBlue"];

    public Color PrepaidCardBackground => IsPrepaidSelected
        ? (Color)Microsoft.Maui.Controls.Application.Current!.Resources["DoorOrange"]
        : (Color)Microsoft.Maui.Controls.Application.Current!.Resources["White"];

    public Color PrepaidCardBorder => IsPrepaidSelected
        ? (Color)Microsoft.Maui.Controls.Application.Current!.Resources["DoorOrange"]
        : (Color)Microsoft.Maui.Controls.Application.Current!.Resources["DoorBlue10"];

    public Color PrepaidCardText => IsPrepaidSelected
        ? Colors.White
        : (Color)Microsoft.Maui.Controls.Application.Current!.Resources["DoorBlue"];

    private bool IsPayPalSelected => string.Equals(_selectedMethod, "PayPal", StringComparison.OrdinalIgnoreCase);

    private bool IsPrepaidSelected => string.Equals(_selectedMethod, "PrepaidCard", StringComparison.OrdinalIgnoreCase);

    public PaymentsPage()
    {
        InitializeComponent();
        BindingContext = this;
        LoadPreferences();
    }

    protected override Task RefreshLanguageDataAsync()
    {
        OnSelectionChanged();
        OnPropertyChanged(string.Empty);
        return Task.CompletedTask;
    }

    private void LoadPreferences()
    {
        var method = Preferences.Default.Get(PreferredPaymentKey, "PayPal");
        _selectedMethod = method is "PrepaidCard" or "PayPal" ? method : "PayPal";
        OnSelectionChanged();
    }

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(PayPalCardBackground));
        OnPropertyChanged(nameof(PayPalCardBorder));
        OnPropertyChanged(nameof(PayPalCardText));
        OnPropertyChanged(nameof(PrepaidCardBackground));
        OnPropertyChanged(nameof(PrepaidCardBorder));
        OnPropertyChanged(nameof(PrepaidCardText));
    }

    private void OnPayPalTapped(object sender, TappedEventArgs e)
    {
        _selectedMethod = "PayPal";
        OnSelectionChanged();
    }

    private void OnPrepaidTapped(object sender, TappedEventArgs e)
    {
        _selectedMethod = "PrepaidCard";
        OnSelectionChanged();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (_selectedMethod == "PrepaidCard" && !string.IsNullOrWhiteSpace(PrepaidCode))
        {
            if (!IsValidPrepaidInput(PrepaidCode))
            {
                await DisplayAlert(AppText.Get("ValidationTitle"), AppText.Get("PrepaidCodeRequiredHint"), AppText.Get("Ok"));
                return;
            }

            PrepaidCode = NormalizePrepaidPayload(PrepaidCode);
        }

        Preferences.Default.Set(PreferredPaymentKey, _selectedMethod);
        await DisplayAlert(AppText.Get("PaymentTitle"), AppText.Get("PaymentPreferencesSaved"), AppText.Get("Ok"));
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await NavigationHelper.PopAsync();
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
}
