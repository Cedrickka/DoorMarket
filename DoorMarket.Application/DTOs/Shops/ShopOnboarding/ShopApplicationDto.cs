namespace DoorMarket.Application.DTOs.ShopOnboarding;

public record CreateShopApplicationRequest(string Name, string CountryTag, string City, string? ImageUrl = null);

public record ShopApplicationDocumentDto(
    Guid Id, string DocType, string FileName, string PublicUrl, long SizeBytes, DateTime CreatedAtUtc);

public record ShopApplicationDto(
    Guid Id,
    string Status,
    string Name,
    string? ImageUrl,
    string CountryTag,
    string City,
    string? ReviewNote,
    DateTime CreatedAtUtc,
    IReadOnlyList<ShopApplicationDocumentDto> Documents);

public record AdminReviewShopApplicationRequest(string Action, string? Note); // Approve|Reject
