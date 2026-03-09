namespace DoorMarket.Mobile.Services;

public sealed record PayPalCheckoutResult(string Url, string CheckoutId);
public sealed record StripeCheckoutResult(string Url, string SessionId);

public sealed record PrepaidPaymentResult(bool Paid, Guid OrderId, string PaymentStatus, string Message);

public sealed class PaymentsApiClient : ApiClientBase
{
    public PaymentsApiClient(HttpClient http) : base(http)
    {
    }

    public Task<PayPalCheckoutResult> CreatePayPalCheckoutAsync(Guid orderId, CancellationToken ct)
        => PostAsync<PayPalCheckoutResult>($"api/payments/paypal/create-checkout?orderId={orderId}", new { }, ct);

    public Task<StripeCheckoutResult> CreateStripeCheckoutSessionAsync(Guid orderId, CancellationToken ct)
        => PostAsync<StripeCheckoutResult>($"api/payments/stripe/checkout-session?orderId={orderId}", new { }, ct);

    public Task<PrepaidPaymentResult> PayWithPrepaidCardAsync(Guid orderId, string cardCode, CancellationToken ct)
        => PostAsync<PrepaidPaymentResult>("api/payments/prepaid/pay", new PrepaidPaymentRequest(orderId, cardCode), ct);

    private sealed record PrepaidPaymentRequest(Guid OrderId, string CardCode);
}
