using DoorMarket.Api.Services;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Payments;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("checkout")]
public class PaymentsController : ControllerBase
{
    private readonly IStripePaymentService _stripe;
    private readonly IPayPalPaymentService _payPal;
    private readonly IMobileMoneyPaymentService _mobileMoney;
    private readonly IOrderPaymentWorkflowService _workflow;
    private readonly ICheckoutObservabilityService _checkoutObservability;
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;

    public PaymentsController(
        IStripePaymentService stripe,
        IPayPalPaymentService payPal,
        IMobileMoneyPaymentService mobileMoney,
        IOrderPaymentWorkflowService workflow,
        ICheckoutObservabilityService checkoutObservability,
        DoorMarketDbContext db,
        ICurrentUserService current)
    {
        _stripe = stripe;
        _payPal = payPal;
        _mobileMoney = mobileMoney;
        _workflow = workflow;
        _checkoutObservability = checkoutObservability;
        _db = db;
        _current = current;
    }

    [HttpPost("stripe/checkout-session")]
    public async Task<IActionResult> CreateStripeCheckoutSession([FromQuery] Guid orderId, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _stripe.CreateCheckoutSessionAsync(orderId, ct);
            sw.Stop();
            await TrackSafeAsync(
                CheckoutObservabilityEvents.PaymentInitiated,
                orderId: orderId,
                provider: "Stripe",
                channel: "External",
                success: true,
                durationMs: (int)sw.ElapsedMilliseconds,
                metadata: new { result.sessionId },
                ct: ct);
            return Ok(new { url = result.url, sessionId = result.sessionId });
        }
        catch (Exception ex)
        {
            sw.Stop();
            await TrackSafeAsync(
                CheckoutObservabilityEvents.PaymentInitiationFailed,
                orderId: orderId,
                provider: "Stripe",
                channel: "External",
                success: false,
                durationMs: (int)sw.ElapsedMilliseconds,
                errorCode: "stripe_checkout_session_error",
                errorMessage: ex.Message,
                ct: ct);
            throw;
        }
    }

    // Backward compatibility for current Web screens.
    [HttpPost("stripe/create-checkout")]
    public Task<IActionResult> CreateCheckout([FromQuery] Guid orderId, CancellationToken ct)
        => CreateStripeCheckoutSession(orderId, ct);

    [HttpPost("paypal/create-order")]
    public async Task<IActionResult> CreatePayPalOrder([FromQuery] Guid orderId, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var (approvalUrl, payPalOrderId) = await _payPal.CreateOrderAsync(orderId, ct);
            sw.Stop();
            await TrackSafeAsync(
                CheckoutObservabilityEvents.PaymentInitiated,
                orderId: orderId,
                provider: "PayPal",
                channel: "External",
                success: true,
                durationMs: (int)sw.ElapsedMilliseconds,
                metadata: new { payPalOrderId },
                ct: ct);
            return Ok(new { url = approvalUrl, payPalOrderId });
        }
        catch (Exception ex)
        {
            sw.Stop();
            await TrackSafeAsync(
                CheckoutObservabilityEvents.PaymentInitiationFailed,
                orderId: orderId,
                provider: "PayPal",
                channel: "External",
                success: false,
                durationMs: (int)sw.ElapsedMilliseconds,
                errorCode: "paypal_create_order_error",
                errorMessage: ex.Message,
                ct: ct);
            throw;
        }
    }

    // Backward compatibility for current Web screens.
    [HttpPost("paypal/create-checkout")]
    public Task<IActionResult> CreatePayPalCheckout([FromQuery] Guid orderId, CancellationToken ct)
        => CreatePayPalOrder(orderId, ct);

    [HttpPost("paypal/capture")]
    public async Task<IActionResult> CapturePayPal([FromBody] PayPalCaptureRequest request, CancellationToken ct)
    {
        if (request.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(request.PayPalOrderId))
        {
            return BadRequest(new { paid = false, message = "Donnees PayPal invalides." });
        }

        var capture = await _payPal.CaptureOrderAsync(request.OrderId, request.PayPalOrderId, ct);
        if (!capture.Paid)
        {
            await TrackSafeAsync(
                CheckoutObservabilityEvents.PaymentFailed,
                orderId: request.OrderId,
                provider: "PayPal",
                channel: "External",
                success: false,
                errorCode: capture.PaymentStatus,
                errorMessage: capture.Message,
                metadata: new { request.PayPalOrderId, capture.CaptureId },
                ct: ct);
            return Ok(new { paid = false, paymentStatus = capture.PaymentStatus, message = capture.Message });
        }

        var workflow = await _workflow.MarkOrderPaidAsync(
            request.OrderId,
            "PayPal",
            new PaymentSnapshotInput(
                CardBrand: ExtractBrand(capture.FundingSource),
                Last4: null,
                ExpMonth: null,
                ExpYear: null,
                Country: null,
                Funding: capture.FundingSource,
                ProviderPaymentIntentId: request.PayPalOrderId,
                ProviderChargeId: capture.CaptureId),
            ct);
        await TrackPayPalCaptureResultAsync(request, workflow, capture.CaptureId, ct);

        return Ok(new
        {
            paid = workflow.Paid,
            orderId = request.OrderId,
            paymentStatus = workflow.PaymentStatus,
            message = workflow.Message
        });
    }

    [NonAction]
    private async Task TrackPayPalCaptureResultAsync(
        PayPalCaptureRequest request,
        PaymentWorkflowResult workflow,
        string? captureId,
        CancellationToken ct)
    {
        var isPaid = workflow.Paid && string.Equals(workflow.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase);
        await TrackSafeAsync(
            isPaid ? CheckoutObservabilityEvents.PaymentConfirmed : CheckoutObservabilityEvents.PaymentFailed,
            orderId: request.OrderId,
            provider: "PayPal",
            channel: "External",
            success: isPaid,
            errorCode: isPaid ? null : workflow.PaymentStatus,
            errorMessage: isPaid ? null : workflow.Message,
            metadata: new { request.PayPalOrderId, captureId },
            ct: ct);
    }

    [HttpPost("prepaid/pay")]
    public async Task<IActionResult> PayWithPrepaid([FromBody] PrepaidPaymentRequest request, CancellationToken ct)
    {
        var inputCode = string.IsNullOrWhiteSpace(request.VoucherCode) ? request.CardCode : request.VoucherCode;
        if (request.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(inputCode))
        {
            await TrackSafeAsync(
                CheckoutObservabilityEvents.PaymentInitiationFailed,
                orderId: request.OrderId == Guid.Empty ? null : request.OrderId,
                provider: "PrepaidInternal",
                channel: "Prepaid",
                success: false,
                errorCode: "invalid_prepaid_request",
                errorMessage: "Donnees de paiement invalides.",
                ct: ct);
            return BadRequest(new { paid = false, message = "Donnees de paiement invalides." });
        }

        var normalizedCode = NormalizePrepaidInput(inputCode);
        if (!IsValidPrepaidVoucher(normalizedCode))
        {
            await TrackSafeAsync(
                CheckoutObservabilityEvents.PaymentFailed,
                orderId: request.OrderId,
                provider: "PrepaidInternal",
                channel: "Prepaid",
                success: false,
                errorCode: "invalid_prepaid_code",
                errorMessage: "Code prepayee invalide.",
                ct: ct);
            return BadRequest(new { paid = false, message = "Code prepayee invalide." });
        }

        var order = await GetMyOrderAsync(request.OrderId, ct);
        if (string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            await TrackSafeAsync(
                CheckoutObservabilityEvents.PaymentConfirmed,
                orderId: request.OrderId,
                provider: "PrepaidInternal",
                channel: "Prepaid",
                success: true,
                metadata: new { status = "already_paid" },
                ct: ct);
            return Ok(new { paid = true, orderId = order.Id, paymentStatus = order.PaymentStatus, message = "Commande deja reglee." });
        }

        await TrackSafeAsync(
            CheckoutObservabilityEvents.PaymentInitiated,
            orderId: request.OrderId,
            provider: "PrepaidInternal",
            channel: "Prepaid",
            success: true,
            ct: ct);

        var workflow = await _workflow.MarkOrderPaidAsync(
            request.OrderId,
            "PrepaidInternal",
            new PaymentSnapshotInput(
                CardBrand: "Voucher",
                Last4: normalizedCode.Length >= 4 ? normalizedCode[^4..] : normalizedCode,
                ExpMonth: null,
                ExpYear: null,
                Country: null,
                Funding: "wallet",
                ProviderPaymentIntentId: normalizedCode,
                ProviderChargeId: null),
            ct);

        await TrackSafeAsync(
            workflow.Paid ? CheckoutObservabilityEvents.PaymentConfirmed : CheckoutObservabilityEvents.PaymentFailed,
            orderId: request.OrderId,
            provider: "PrepaidInternal",
            channel: "Prepaid",
            success: workflow.Paid,
            errorCode: workflow.Paid ? null : workflow.PaymentStatus,
            errorMessage: workflow.Paid ? null : workflow.Message,
            ct: ct);

        return Ok(new
        {
            paid = workflow.Paid,
            orderId = request.OrderId,
            paymentStatus = workflow.PaymentStatus,
            message = workflow.Message
        });
    }

    [HttpPost("mobile-money/initiate")]
    public async Task<IActionResult> InitiateMobileMoney([FromBody] MobileMoneyInitiateRequest request, CancellationToken ct)
    {
        if (request.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            await TrackSafeAsync(
                CheckoutObservabilityEvents.PaymentInitiationFailed,
                orderId: request.OrderId == Guid.Empty ? null : request.OrderId,
                provider: request.Provider,
                channel: "MobileMoney",
                success: false,
                errorCode: "invalid_mobile_money_request",
                errorMessage: "OrderId, Provider et PhoneNumber sont requis.",
                ct: ct);
            return BadRequest(new { message = "OrderId, Provider et PhoneNumber sont requis." });
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var checkout = await _mobileMoney.InitiateAsync(
                request.OrderId,
                request.Provider,
                request.PhoneNumber,
                request.CallbackUrl,
                ct);
            sw.Stop();
            await TrackSafeAsync(
                CheckoutObservabilityEvents.PaymentInitiated,
                orderId: request.OrderId,
                provider: checkout.Provider,
                channel: "MobileMoney",
                success: true,
                durationMs: (int)sw.ElapsedMilliseconds,
                metadata: new { checkout.TransactionId, checkout.Status, checkout.CheckoutUrl },
                ct: ct);

            return Ok(new
            {
                provider = checkout.Provider,
                transactionId = checkout.TransactionId,
                status = checkout.Status,
                checkoutUrl = checkout.CheckoutUrl,
                message = checkout.Message
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            await TrackSafeAsync(
                CheckoutObservabilityEvents.PaymentInitiationFailed,
                orderId: request.OrderId,
                provider: request.Provider,
                channel: "MobileMoney",
                success: false,
                durationMs: (int)sw.ElapsedMilliseconds,
                errorCode: "mobile_money_initiate_error",
                errorMessage: ex.Message,
                ct: ct);
            throw;
        }
    }

    [HttpPost("mobile-money/confirm")]
    public async Task<IActionResult> ConfirmMobileMoney([FromBody] MobileMoneyConfirmRequest request, CancellationToken ct)
    {
        if (request.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.TransactionId))
        {
            await TrackSafeAsync(
                CheckoutObservabilityEvents.PaymentFailed,
                orderId: request.OrderId == Guid.Empty ? null : request.OrderId,
                provider: request.Provider,
                channel: "MobileMoney",
                success: false,
                errorCode: "invalid_mobile_money_confirm_request",
                errorMessage: "OrderId, Provider et TransactionId sont requis.",
                ct: ct);
            return BadRequest(new { paid = false, message = "OrderId, Provider et TransactionId sont requis." });
        }

        var status = await _mobileMoney.CheckStatusAsync(request.OrderId, request.Provider, request.TransactionId, ct);
        if (!status.Paid)
        {
            await TrackSafeAsync(
                CheckoutObservabilityEvents.PaymentFailed,
                orderId: request.OrderId,
                provider: status.Provider,
                channel: "MobileMoney",
                success: false,
                errorCode: status.Status,
                errorMessage: status.Message,
                metadata: new { request.TransactionId, status.RawStatus },
                ct: ct);
            return Ok(new
            {
                paid = false,
                paymentStatus = status.Status,
                provider = status.Provider,
                transactionId = status.TransactionId,
                rawStatus = status.RawStatus,
                message = status.Message ?? "Paiement en attente."
            });
        }

        var workflow = await _workflow.MarkOrderPaidAsync(
            request.OrderId,
            $"MobileMoney:{status.Provider}",
            new PaymentSnapshotInput(
                CardBrand: status.Provider,
                Last4: null,
                ExpMonth: null,
                ExpYear: null,
                Country: null,
                Funding: "mobile_money",
                ProviderPaymentIntentId: request.TransactionId,
                ProviderChargeId: null),
            ct);

        await TrackSafeAsync(
            workflow.Paid ? CheckoutObservabilityEvents.PaymentConfirmed : CheckoutObservabilityEvents.PaymentFailed,
            orderId: request.OrderId,
            provider: status.Provider,
            channel: "MobileMoney",
            success: workflow.Paid,
            errorCode: workflow.Paid ? null : workflow.PaymentStatus,
            errorMessage: workflow.Paid ? null : workflow.Message,
            metadata: new { request.TransactionId, status.RawStatus },
            ct: ct);

        return Ok(new
        {
            paid = workflow.Paid,
            orderId = request.OrderId,
            paymentStatus = workflow.PaymentStatus,
            provider = status.Provider,
            transactionId = status.TransactionId,
            rawStatus = status.RawStatus,
            message = workflow.Message
        });
    }

    private async Task<Domain.Entities.Order> GetMyOrderAsync(Guid orderId, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");

        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);

        return order ?? throw new InvalidOperationException("Commande introuvable.");
    }

    private static string NormalizePrepaidInput(string? value)
    {
        var raw = (value ?? string.Empty).Trim().ToUpperInvariant();
        return raw.Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private static bool IsValidPrepaidVoucher(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        if (code.Length < 8 || code.Length > 40)
        {
            return false;
        }

        foreach (var c in code)
        {
            if (!(char.IsLetterOrDigit(c) || c == '-'))
            {
                return false;
            }
        }

        return code.StartsWith("DM-", StringComparison.Ordinal);
    }

    private static string? ExtractBrand(string? fundingSource)
    {
        if (string.IsNullOrWhiteSpace(fundingSource))
        {
            return null;
        }

        if (fundingSource.Contains(":", StringComparison.Ordinal))
        {
            return fundingSource.Split(':', 2)[1];
        }

        return fundingSource;
    }

    public sealed record PrepaidPaymentRequest(Guid OrderId, string? VoucherCode, string? CardCode);
    public sealed record PayPalCaptureRequest(Guid OrderId, string PayPalOrderId);
    public sealed record MobileMoneyInitiateRequest(Guid OrderId, string Provider, string PhoneNumber, string? CallbackUrl);
    public sealed record MobileMoneyConfirmRequest(Guid OrderId, string Provider, string TransactionId);

    private async Task TrackSafeAsync(
        string eventType,
        Guid? orderId,
        string? provider,
        string? channel,
        bool? success,
        int? durationMs = null,
        string? errorCode = null,
        string? errorMessage = null,
        object? metadata = null,
        CancellationToken ct = default)
    {
        try
        {
            await _checkoutObservability.TrackAsync(
                eventType: eventType,
                orderId: orderId,
                paymentProvider: provider,
                paymentChannel: channel,
                success: success,
                durationMs: durationMs,
                errorCode: errorCode,
                errorMessage: errorMessage,
                source: "Api",
                metadata: metadata,
                ct: ct);
        }
        catch
        {
            // Tracking failures must never block payment flow.
        }
    }
}

