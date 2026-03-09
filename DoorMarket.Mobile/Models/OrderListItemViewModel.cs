using Microsoft.Maui.Graphics;

namespace DoorMarket.Mobile.Models;

public sealed class OrderListItemViewModel
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string DateLabel { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public Color StatusBackgroundColor { get; init; } = Colors.Gray;
    public Color StatusBorderColor { get; init; } = Colors.Gray;
    public Color StatusTextColor { get; init; } = Colors.White;
    public string TotalLabel { get; init; } = string.Empty;
    public string StatusSubtitle { get; init; } = string.Empty;
    public string PaymentProviderLabel { get; init; } = string.Empty;
    public bool NeedsPaymentAction { get; init; }
    public bool HasStatusSubtitle => !string.IsNullOrWhiteSpace(StatusSubtitle);
    public bool HasPaymentProvider => !string.IsNullOrWhiteSpace(PaymentProviderLabel);
}
