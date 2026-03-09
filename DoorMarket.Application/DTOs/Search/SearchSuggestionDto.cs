namespace DoorMarket.Application.DTOs.Search;

public sealed record SearchSuggestionDto(
    string Type,
    Guid? EntityId,
    string Value,
    string Label,
    string? Subtitle
);
