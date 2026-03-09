using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public sealed class SearchQueryRuleConfiguration : IEntityTypeConfiguration<SearchQueryRule>
{
    public void Configure(EntityTypeBuilder<SearchQueryRule> b)
    {
        b.ToTable("SearchQueryRules");

        b.HasKey(x => x.Id);

        b.Property(x => x.TriggerQuery).HasMaxLength(120).IsRequired();
        b.Property(x => x.CanonicalQuery).HasMaxLength(120);
        b.Property(x => x.TargetType).HasMaxLength(20);
        b.Property(x => x.TargetId);
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => x.TriggerQuery).IsUnique();
        b.HasIndex(x => new { x.IsActive, x.TriggerQuery });
        b.HasIndex(x => new { x.TargetType, x.TargetId, x.IsActive });
    }
}
