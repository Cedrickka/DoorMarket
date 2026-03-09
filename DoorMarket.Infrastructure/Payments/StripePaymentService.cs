using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Payments;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stripe;

namespace DoorMarket.Infrastructure.Payments;

public class StripePaymentService : IStripePaymentService
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IConfiguration _config;

    public StripePaymentService(DoorMarketDbContext db, ICurrentUserService current, IConfiguration config)
    {
        _db = db;
        _current = current;
        _config = config;
    }

    public Task<(string clientSecret, string paymentIntentId)> CreatePaymentIntentAsync(Guid orderId, CancellationToken ct)
        => throw new NotSupportedException("V1: on utilise Stripe Checkout (redirect).");

    public async Task<(string url, string sessionId)> CreateCheckoutSessionAsync(Guid orderId, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var order = await _db.Orders
            .AsTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct)
            ?? throw new InvalidOperationException("Commande introuvable.");

        if (order.PaymentStatus == "Paid")
            throw new InvalidOperationException("Commande déjà payée.");

        var amount = (long)Math.Round(order.TotalAmount * 100m, 0);
        if (amount <= 0) throw new InvalidOperationException("Montant invalide.");

        var successTpl = _config["Stripe:CheckoutSuccessUrl"] ?? throw new InvalidOperationException("Stripe:CheckoutSuccessUrl manquant.");
        var cancelTpl = _config["Stripe:CheckoutCancelUrl"] ?? throw new InvalidOperationException("Stripe:CheckoutCancelUrl manquant.");

        var successUrl = successTpl.Replace("{ORDER_ID}", order.Id.ToString());
        var cancelUrl = cancelTpl.Replace("{ORDER_ID}", order.Id.ToString());

        var service = new Stripe.Checkout.SessionService();
        var session = await service.CreateAsync(new Stripe.Checkout.SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,

            LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
            {
                new Stripe.Checkout.SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                    {
                        Currency = order.Currency.ToLowerInvariant(),
                        UnitAmount = amount,
                        ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"DoorMarket Order #{order.Id.ToString("N")[..8]}",
                            Description = $"{order.DeliveryCity}, {order.DeliveryCountry}"
                        }
                    }
                }
            },

            Metadata = new Dictionary<string, string>
            {
                ["orderId"] = order.Id.ToString(),
                ["userId"] = userId.ToString()
            }
        }, cancellationToken: ct);

        order.StripeCheckoutSessionId = session.Id;
        order.StripeCheckoutPaymentIntentId = session.PaymentIntentId;
        order.PaymentProvider = "Stripe";
        order.PaymentStatus = "Pending";
        order.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (string.IsNullOrWhiteSpace(session.Url))
            throw new InvalidOperationException("Stripe n'a pas retourné d'URL de checkout.");

        return (session.Url, session.Id);
    }
}
