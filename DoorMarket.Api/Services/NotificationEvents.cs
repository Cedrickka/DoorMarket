namespace DoorMarket.Api.Services;

public static class NotificationEvents
{
    public const string ChannelEmail = "Email";
    public const string ChannelPush = "Push";

    public const string StatusSent = "Sent";
    public const string StatusFailed = "Failed";
    public const string StatusSkipped = "Skipped";

    public const string ClientOrderCreated = "ClientOrderCreated";
    public const string ClientPaymentPaid = "ClientPaymentPaid";
    public const string ClientPaymentFailed = "ClientPaymentFailed";
    public const string ShopOrderPaidPending = "ShopOrderPaidPending";
    public const string AdminOrderPaid = "AdminOrderPaid";
    public const string CartAbandonedReminderEmail = "CartAbandonedReminderEmail";
    public const string CartAbandonedReminderPush = "CartAbandonedReminderPush";
}
