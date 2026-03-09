using DoorMarket.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/checkout/analytics")]
[AllowAnonymous]
public class CheckoutAnalyticsController : ControllerBase
{
    private readonly ICheckoutObservabilityService _tracking;

    public CheckoutAnalyticsController(ICheckoutObservabilityService tracking)
    {
        _tracking = tracking;
    }

    [HttpPost("track")]
    public async Task<ActionResult<CheckoutAnalyticsTrackedDto>> Track(
        [FromBody] TrackCheckoutEventRequest request,
        CancellationToken ct = default)
    {
        var eventType = NormalizeEventType(request.EventName);
        object? metadata = request.Metadata.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? null
            : request.Metadata;

        var row = await _tracking.TrackAsync(
            eventType: eventType,
            orderId: request.OrderId,
            sessionId: request.SessionId,
            paymentProvider: request.PaymentProvider,
            paymentChannel: request.PaymentChannel,
            experimentName: request.ExperimentName,
            experimentGroup: request.ExperimentGroup,
            success: request.Success,
            durationMs: request.DurationMs,
            errorCode: request.ErrorCode,
            errorMessage: request.ErrorMessage,
            source: request.Source,
            countryTag: request.CountryTag,
            metadata: metadata,
            ct: ct);

        return Ok(new CheckoutAnalyticsTrackedDto(
            row.Id,
            row.EventType,
            row.OccurredAtUtc));
    }

    private static string NormalizeEventType(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "checkout_view" or "checkout_opened" => CheckoutObservabilityEvents.CheckoutViewed,
            "checkout_payment_method_selected" or "payment_method_selected" => CheckoutObservabilityEvents.CheckoutMethodSelected,
            "checkout_submit" or "checkout_submit_clicked" => CheckoutObservabilityEvents.CheckoutSubmitClicked,
            "checkout_order_created" or "order_created" => CheckoutObservabilityEvents.OrderCreated,
            "checkout_payment_initiated" or "payment_initiated" => CheckoutObservabilityEvents.PaymentInitiated,
            "checkout_payment_initiation_failed" or "payment_init_failed" => CheckoutObservabilityEvents.PaymentInitiationFailed,
            "checkout_payment_redirect_opened" or "payment_redirect_opened" => CheckoutObservabilityEvents.PaymentRedirectOpened,
            "checkout_payment_redirect_failed" or "payment_redirect_failed" => CheckoutObservabilityEvents.PaymentRedirectFailed,
            "checkout_payment_confirmed" or "payment_confirmed" => CheckoutObservabilityEvents.PaymentConfirmed,
            "checkout_payment_failed" or "payment_failed" => CheckoutObservabilityEvents.PaymentFailed,
            _ => normalized.Length <= 40 ? normalized : normalized[..40]
        };
    }

    public sealed record TrackCheckoutEventRequest(
        string? EventName,
        Guid? OrderId,
        string? SessionId,
        string? PaymentProvider,
        string? PaymentChannel,
        string? ExperimentName,
        string? ExperimentGroup,
        bool? Success,
        int? DurationMs,
        string? ErrorCode,
        string? ErrorMessage,
        string? Source,
        string? CountryTag,
        JsonElement Metadata);

    public sealed record CheckoutAnalyticsTrackedDto(
        Guid EventId,
        string EventType,
        DateTime OccurredAtUtc);
}
