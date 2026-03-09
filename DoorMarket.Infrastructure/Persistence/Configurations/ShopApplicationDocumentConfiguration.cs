using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class ShopApplicationDocumentConfiguration : IEntityTypeConfiguration<ShopApplicationDocument>
{
    public void Configure(EntityTypeBuilder<ShopApplicationDocument> b)
    {
        b.ToTable("ShopApplicationDocuments");
        b.HasKey(x => x.Id);

        b.Property(x => x.DocType).HasMaxLength(30).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.StoragePath).HasMaxLength(400).IsRequired();
        b.Property(x => x.PublicUrl).HasMaxLength(600).IsRequired();

        b.HasIndex(x => new { x.ShopApplicationId, x.DocType });
    }
}
