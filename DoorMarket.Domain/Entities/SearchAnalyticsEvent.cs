using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class SearchAnalyticsEvent : AuditableEntity
{
    public Guid? UserId { get; set; }
    public string? SessionId { get; set; }
    public string EventType { get; set; } = "Query"; // Query | Click
    public string? Query { get; set; }
    public string? NormalizedQuery { get; set; }
    public string? TargetType { get; set; } // Product | Shop | Category
    public Guid? TargetId { get; set; }
    public int? Position { get; set; }
    public int? ResultsCount { get; set; }
    public int? DurationMs { get; set; }
    public int? Page { get; set; }
    public string? Sort { get; set; }
    public string? FiltersHash { get; set; }
    public string? Source { get; set; } // Web | Mobile
    public string? CountryTag { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
