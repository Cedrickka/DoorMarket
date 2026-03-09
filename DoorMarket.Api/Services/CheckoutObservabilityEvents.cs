namespace DoorMarket.Api.Services;

public static class CheckoutObservabilityEvents
{
    public const string CheckoutViewed = "checkout_viewed";
    public const string CheckoutMethodSelected = "checkout_payment_method_selected";
    public const string CheckoutSubmitClicked = "checkout_submit_clicked";
    public const string OrderCreated = "checkout_order_created";
    public const string PaymentInitiated = "checkout_payment_initiated";
    public const string PaymentInitiationFailed = "checkout_payment_initiation_failed";
    public const string PaymentRedirectOpened = "checkout_payment_redirect_opened";
    public const string PaymentRedirectFailed = "checkout_payment_redirect_failed";
    public const string PaymentConfirmed = "checkout_payment_confirmed";
    public const string PaymentFailed = "checkout_payment_failed";

    public const string ExperimentCheckoutUxV1 = "checkout_ux_v1";
}
