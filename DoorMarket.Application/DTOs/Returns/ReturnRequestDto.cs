namespace DoorMarket.Application.DTOs.Returns;

public record CreateReturnRequest(
    Guid OrderId,
    string? ReasonCode,
    string Reason,
    string? Comment,
    decimal? RequestedAmount
);

public record ReturnRequestDto(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    string Status,
    string? ReasonCode,
    string Reason,
    string? Comment,
    decimal RequestedAmount,
    decimal? ApprovedAmount,
    string Currency,
    string? AdminNote,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc,
    DateTime? RefundedAtUtc,
    DateTime? SlaTargetAtUtc,
    DateTime? LastStatusChangedAtUtc,
    bool IsSlaBreached
);

public record ReturnReasonDto(
    string Code,
    string TitleFr,
    string TitleEn,
    string? DescriptionFr,
    string? DescriptionEn,
    int DefaultSlaHours,
    int SortOrder
);

public record ReturnTimelineEventDto(
    Guid Id,
    string OldStatus,
    string NewStatus,
    string? Note,
    DateTime ChangedAtUtc,
    string? ChangedBy
);
