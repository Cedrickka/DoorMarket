using DoorMarket.Api.Services;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly DoorMarketDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<WebhooksController> _logger;
    private readonly IOrderPaymentWorkflowService _workflow;

    public WebhooksController(
        DoorMarketDbContext db,
        IConfiguration config,
        ILogger<WebhooksController> logger,
        IOrderPaymentWorkflowService workflow)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _workflow = workflow;
    }

    [HttpPost("stripe")]
    public async Task<IActionResult> StripeWebhook(CancellationToken ct)
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync(ct);
        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
        var webhookSecret = _config["Stripe:WebhookSecret"];

        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            _logger.LogError("Stripe webhook secret missing in configuration.");
            return StatusCode(500);
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid Stripe signature.");
            return BadRequest();
        }

        var webhookRow = await _db.ProcessedWebhookEvents
            .FirstOrDefaultAsync(x => x.Provider == "Stripe" && x.EventId == stripeEvent.Id, ct);

        if (webhookRow is not null && webhookRow.ProcessedAtUtc is not null)
        {
            return Ok();
        }

        if (webhookRow is null)
        {
            webhookRow = new Domain.Entities.ProcessedWebhookEvent
            {
                Provider = "Stripe",
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                ReceivedAtUtc = DateTime.UtcNow,
                ProcessedAtUtc = null
            };

            _db.ProcessedWebhookEvents.Add(webhookRow);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return Ok();
            }
        }

        try
        {
            if (stripeEvent.Type == "checkout.session.completed")
            {
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                if (session is not null)
                {
                    var orderIdStr = session.Metadata != null && session.Metadata.TryGetValue("orderId", out var value)
                        ? value
                        : null;

                    if (Guid.TryParse(orderIdStr, out var orderId))
                    {
                        var snapshot = await BuildStripeSnapshotAsync(session, ct);
                        await _workflow.MarkOrderPaidAsync(orderId, "Stripe", snapshot, ct);
                    }
                }
            }
            else if (stripeEvent.Type == "checkout.session.async_payment_failed")
            {
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                var orderIdStr = session?.Metadata != null && session.Metadata.TryGetValue("orderId", out var value)
                    ? value
                    : null;

                if (Guid.TryParse(orderIdStr, out var orderId))
                {
                    await _workflow.MarkOrderFailedAsync(orderId, ct);
                }
            }

            webhookRow.EventType = stripeEvent.Type;
            webhookRow.ProcessedAtUtc = DateTime.UtcNow;
            webhookRow.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stripe webhook processing failed. eventType={EventType}", stripeEvent.Type);
            return StatusCode(500);
        }
    }

    private static async Task<PaymentSnapshotInput?> BuildStripeSnapshotAsync(Stripe.Checkout.Session session, CancellationToken ct)
    {
        var paymentIntentId = session.PaymentIntentId;
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return null;
        }

        var intentService = new PaymentIntentService();
        var intent = await intentService.GetAsync(paymentIntentId, cancellationToken: ct);

        string? chargeId = null;
        string? brand = null;
        string? last4 = null;
        int? expMonth = null;
        int? expYear = null;
        string? country = null;
        string? funding = null;

        if (!string.IsNullOrWhiteSpace(intent.LatestChargeId))
        {
            var chargeService = new ChargeService();
            var charge = await chargeService.GetAsync(intent.LatestChargeId, cancellationToken: ct);
            chargeId = charge.Id;

            var card = charge.PaymentMethodDetails?.Card;
            if (card is not null)
            {
                brand = card.Brand;
                last4 = card.Last4;
                expMonth = (int)card.ExpMonth;
                expYear = (int)card.ExpYear;
                country = card.Country;
                funding = card.Funding;
            }
        }

        return new PaymentSnapshotInput(
            CardBrand: brand,
            Last4: last4,
            ExpMonth: expMonth,
            ExpYear: expYear,
            Country: country,
            Funding: funding,
            ProviderPaymentIntentId: intent.Id,
            ProviderChargeId: chargeId);
    }
}
