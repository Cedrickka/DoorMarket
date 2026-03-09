using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class UserPushDevice : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public string Platform { get; set; } = "fcm"; // fcm | apns
    public string Token { get; set; } = "";
    public string? DeviceId { get; set; }
    public string? DeviceModel { get; set; }
    public string? AppVersion { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
}

