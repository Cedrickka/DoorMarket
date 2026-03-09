using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class PaymentMethodSnapshotConfiguration : IEntityTypeConfiguration<PaymentMethodSnapshot>
{
    public void Configure(EntityTypeBuilder<PaymentMethodSnapshot> b)
    {
        b.ToTable("PaymentMethodSnapshots");

        b.HasKey(x => x.Id);
        b.HasIndex(x => x.OrderId).IsUnique();

        b.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        b.Property(x => x.CardBrand).HasMaxLength(30);
        b.Property(x => x.Last4).HasMaxLength(4);
        b.Property(x => x.Country).HasMaxLength(8);
        b.Property(x => x.Funding).HasMaxLength(30);
        b.Property(x => x.ProviderPaymentIntentId).HasMaxLength(200);
        b.Property(x => x.ProviderChargeId).HasMaxLength(200);
    }
}
