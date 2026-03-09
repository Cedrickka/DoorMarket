using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders");

        b.HasKey(x => x.Id);

        b.Property(x => x.Status).HasMaxLength(40).IsRequired();

        b.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.TotalItemsAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        b.Property(x => x.PlatformFeeTotal).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });

        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasMany(x => x.Items)
         .WithOne(x => x.Order)
         .HasForeignKey(x => x.OrderId)
         .OnDelete(DeleteBehavior.Cascade);

        b.Property(x => x.Subtotal).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.DeliveryFee).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.Discount).HasColumnType("decimal(18,2)").IsRequired();

        b.Property(x => x.PromoCode).HasMaxLength(50);

        b.Property(x => x.DeliveryName).HasMaxLength(120).IsRequired();
        b.Property(x => x.DeliveryPhone).HasMaxLength(40).IsRequired();
        b.Property(x => x.DeliveryLine1).HasMaxLength(200).IsRequired();
        b.Property(x => x.DeliveryCity).HasMaxLength(120).IsRequired();
        b.Property(x => x.DeliveryCountry).HasMaxLength(60).IsRequired();
        b.Property(x => x.DeliveryNotes).HasMaxLength(500);

        b.Property(x => x.PaymentProvider).HasMaxLength(30).IsRequired();
        b.Property(x => x.PaymentStatus).HasMaxLength(30).IsRequired();
        b.Property(x => x.FulfillmentStatus).HasMaxLength(30).IsRequired().HasDefaultValue("PendingPayment");
        b.Property(x => x.StripePaymentIntentId).HasMaxLength(100);
        b.Property(x => x.PaidAtUtc);
        b.Property(x => x.DeliveredAtUtc);
        b.Property(x => x.NotifiedShopPaidPendingAt);
        b.Property(x => x.AdminNotifiedPaid).HasDefaultValue(false);


        b.Property(x => x.StripeCheckoutSessionId).HasMaxLength(200);
        b.Property(x => x.StripeCheckoutPaymentIntentId).HasMaxLength(200);
        b.HasIndex(x => x.StripeCheckoutSessionId).IsUnique()
            .HasFilter("\"StripeCheckoutSessionId\" IS NOT NULL");
        b.HasIndex(x => new { x.PaymentStatus, x.FulfillmentStatus, x.CreatedAtUtc });

        b.HasOne(x => x.PaymentMethodSnapshot)
            .WithOne(x => x.Order)
            .HasForeignKey<PaymentMethodSnapshot>(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.StatusHistory)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
