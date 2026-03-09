using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public sealed class CheckoutAnalyticsEventConfiguration : IEntityTypeConfiguration<CheckoutAnalyticsEvent>
{
    public void Configure(EntityTypeBuilder<CheckoutAnalyticsEvent> b)
    {
        b.ToTable("CheckoutAnalyticsEvents");

        b.HasKey(x => x.Id);

        b.Property(x => x.UserId);
        b.Property(x => x.OrderId);
        b.Property(x => x.SessionId).HasMaxLength(80);
        b.Property(x => x.EventType).HasMaxLength(40).IsRequired();
        b.Property(x => x.PaymentProvider).HasMaxLength(40);
        b.Property(x => x.PaymentChannel).HasMaxLength(40);
        b.Property(x => x.ExperimentName).HasMaxLength(50);
        b.Property(x => x.ExperimentGroup).HasMaxLength(16);
        b.Property(x => x.Success);
        b.Property(x => x.DurationMs);
        b.Property(x => x.ErrorCode).HasMaxLength(80);
        b.Property(x => x.ErrorMessage).HasMaxLength(300);
        b.Property(x => x.Source).HasMaxLength(20);
        b.Property(x => x.CountryTag).HasMaxLength(8);
        b.Property(x => x.MetadataJson).HasMaxLength(2000);
        b.Property(x => x.OccurredAtUtc).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc);

        b.HasIndex(x => new { x.EventType, x.OccurredAtUtc });
        b.HasIndex(x => new { x.Success, x.OccurredAtUtc });
        b.HasIndex(x => new { x.PaymentProvider, x.OccurredAtUtc });
        b.HasIndex(x => new { x.Source, x.OccurredAtUtc });
        b.HasIndex(x => new { x.ExperimentName, x.ExperimentGroup, x.OccurredAtUtc });
        b.HasIndex(x => new { x.OrderId, x.OccurredAtUtc });
        b.HasIndex(x => new { x.UserId, x.OccurredAtUtc });
        b.HasIndex(x => new { x.SessionId, x.OccurredAtUtc });
    }
}
