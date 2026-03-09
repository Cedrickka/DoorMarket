namespace DoorMarket.Application.Interfaces.Payments;

public interface IPayPalPaymentService
{
    Task<(string ApprovalUrl, string PayPalOrderId)> CreateOrderAsync(Guid orderId, CancellationToken ct);
    Task<PayPalCaptureResult> CaptureOrderAsync(Guid orderId, string payPalOrderId, CancellationToken ct);
}

public sealed record PayPalCaptureResult(
    bool Paid,
    string PaymentStatus,
    string Message,
    string? CaptureId,
    string? FundingSource
);
