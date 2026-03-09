using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class Order : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public string Status { get; set; } = "Created"; // V1 simple (Created, Paid, Preparing, Ready, Completed, Cancelled)

    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Discount { get; set; } // V1 simple (0)
    public string? PromoCode { get; set; }

    // Adresse snapshot
    public string DeliveryName { get; set; } = "";
    public string DeliveryPhone { get; set; } = "";
    public string DeliveryLine1 { get; set; } = "";
    public string DeliveryCity { get; set; } = "";
    public string DeliveryCountry { get; set; } = "";
    public string? DeliveryNotes { get; set; }
    public string PaymentProvider { get; set; } = "Stripe";
    public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid | Pending | Paid | Failed
    public string FulfillmentStatus { get; set; } = "PendingPayment"; // PendingPayment | PaidPending | Processing | Delivered | Cancelled
    public string? StripePaymentIntentId { get; set; }
    public string? StripeCheckoutSessionId { get; set; }
    public string? StripeCheckoutPaymentIntentId { get; set; } // optionnel
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? NotifiedShopPaidPendingAt { get; set; }
    public bool AdminNotifiedPaid { get; set; }
    public decimal TotalItemsAmount { get; set; }
    public decimal PlatformFeeTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public List<OrderItem> Items { get; set; } = new();
    public PaymentMethodSnapshot? PaymentMethodSnapshot { get; set; }
    public List<OrderStatusHistory> StatusHistory { get; set; } = new();
    public List<ReturnRequest> ReturnRequests { get; set; } = new();
}
