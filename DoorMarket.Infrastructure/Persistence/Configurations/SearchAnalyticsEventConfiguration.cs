using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public sealed class SearchAnalyticsEventConfiguration : IEntityTypeConfiguration<SearchAnalyticsEvent>
{
    public void Configure(EntityTypeBuilder<SearchAnalyticsEvent> b)
    {
        b.ToTable("SearchAnalyticsEvents");

        b.HasKey(x => x.Id);

        b.Property(x => x.UserId);
        b.Property(x => x.SessionId).HasMaxLength(80);
        b.Property(x => x.EventType).HasMaxLength(20).IsRequired();
        b.Property(x => x.Query).HasMaxLength(120);
        b.Property(x => x.NormalizedQuery).HasMaxLength(120);
        b.Property(x => x.TargetType).HasMaxLength(20);
        b.Property(x => x.TargetId);
        b.Property(x => x.Position);
        b.Property(x => x.ResultsCount);
        b.Property(x => x.DurationMs);
        b.Property(x => x.Page);
        b.Property(x => x.Sort).HasMaxLength(40);
        b.Property(x => x.FiltersHash).HasMaxLength(80);
        b.Property(x => x.Source).HasMaxLength(30);
        b.Property(x => x.CountryTag).HasMaxLength(8);
        b.Property(x => x.OccurredAtUtc).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.EventType, x.OccurredAtUtc });
        b.HasIndex(x => new { x.NormalizedQuery, x.EventType, x.OccurredAtUtc });
        b.HasIndex(x => new { x.Source, x.EventType, x.OccurredAtUtc });
        b.HasIndex(x => new { x.UserId, x.OccurredAtUtc });
        b.HasIndex(x => new { x.SessionId, x.OccurredAtUtc });
    }
}
