namespace DoorMarket.Application.DTOs.Loyalty;

public record LoyaltyWalletDto(
    Guid UserId,
    int PointsBalance,
    int LifetimePointsEarned,
    int LifetimePointsSpent,
    decimal MonetaryBalance,
    string Currency,
    DateTime? LastActivityAtUtc
);

public record LoyaltyLedgerEntryDto(
    Guid Id,
    string EntryType,
    int DeltaPoints,
    decimal DeltaAmount,
    int BalanceAfterPoints,
    decimal BalanceAfterAmount,
    string? SourceType,
    string? SourceId,
    string? Note,
    DateTime CreatedAtUtc
);

public record LoyaltyRuleDto(
    Guid Id,
    string Name,
    bool IsActive,
    int Priority,
    decimal EarnPointsPerUsd,
    decimal? MinOrderAmountUsd,
    decimal RedeemValueUsdPerPoint,
    int MinRedeemPoints,
    decimal MaxRedeemPercentOfOrder,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
