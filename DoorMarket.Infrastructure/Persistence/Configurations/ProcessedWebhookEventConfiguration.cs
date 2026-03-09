using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class ProcessedWebhookEventConfiguration : IEntityTypeConfiguration<ProcessedWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhookEvent> b)
    {
        b.ToTable("ProcessedWebhookEvents");

        b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        b.Property(x => x.EventId).HasMaxLength(200).IsRequired();
        b.Property(x => x.EventType).HasMaxLength(120).IsRequired();
        b.Property(x => x.ReceivedAtUtc).IsRequired();
        b.Property(x => x.ProcessedAtUtc);

        b.HasIndex(x => new { x.Provider, x.EventId }).IsUnique();
    }
}
