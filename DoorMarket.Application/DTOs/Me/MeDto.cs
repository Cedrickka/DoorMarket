namespace DoorMarket.Application.DTOs.Me;

public record MeDto(
    Guid Id,
    string Email,
    string? Phone,
    string? ProfileImageUrl,
    string Role,
    bool IsActive
);

public record UpdateMeRequest(
    string? Phone
);

public record RequestPhoneChangeRequest(
    string? Phone
);

public record ConfirmPhoneChangeRequest(
    string Code
);

public record RequestPasswordChangeRequest(
    string Password
);

public record ConfirmPasswordChangeRequest(
    string Code
);

public record ProfileImageUploadResponse(
    string Url
);



