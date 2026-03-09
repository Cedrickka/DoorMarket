using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class ReturnReasonConfiguration : IEntityTypeConfiguration<ReturnReason>
{
    public void Configure(EntityTypeBuilder<ReturnReason> b)
    {
        b.ToTable("ReturnReasons");
        b.HasKey(x => x.Id);

        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.TitleFr).HasMaxLength(180).IsRequired();
        b.Property(x => x.TitleEn).HasMaxLength(180).IsRequired();
        b.Property(x => x.DescriptionFr).HasMaxLength(1000);
        b.Property(x => x.DescriptionEn).HasMaxLength(1000);
        b.Property(x => x.DefaultSlaHours).IsRequired();
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.SortOrder).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => new { x.IsActive, x.SortOrder });
    }
}
