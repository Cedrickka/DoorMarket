namespace DoorMarket.Application.Interfaces.Payments;

public interface IMobileMoneyPaymentService
{
    Task<MobileMoneyCheckoutResult> InitiateAsync(
        Guid orderId,
        string provider,
        string phoneNumber,
        string? callbackUrl,
        CancellationToken ct);

    Task<MobileMoneyPaymentStatusResult> CheckStatusAsync(
        Guid orderId,
        string provider,
        string transactionId,
        CancellationToken ct);
}

public sealed record MobileMoneyCheckoutResult(
    string Provider,
    string TransactionId,
    string Status,
    string? CheckoutUrl,
    string? Message);

public sealed record MobileMoneyPaymentStatusResult(
    string Provider,
    string TransactionId,
    string Status,
    bool Paid,
    string? Message,
    string? RawStatus);
