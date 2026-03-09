namespace DoorMarket.Application.Interfaces.Payments;

public interface IStripePaymentService
{
    Task<(string clientSecret, string paymentIntentId)> CreatePaymentIntentAsync(Guid orderId, CancellationToken ct);

    Task<(string url, string sessionId)> CreateCheckoutSessionAsync(Guid orderId, CancellationToken ct);
        
}

