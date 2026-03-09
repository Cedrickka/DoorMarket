using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class BankBalanceSnapshotConfiguration : IEntityTypeConfiguration<BankBalanceSnapshot>
{
    public void Configure(EntityTypeBuilder<BankBalanceSnapshot> b)
    {
        b.ToTable("BankBalanceSnapshots");

        b.HasKey(x => x.Id);

        b.Property(x => x.AsOfDateUtc).IsRequired();
        b.Property(x => x.Balance).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.Note).HasMaxLength(400);

        b.HasIndex(x => x.AsOfDateUtc);
        b.Property(x => x.CreatedAtUtc).IsRequired();
    }
}
