using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class ReturnReason : AuditableEntity
{
    public string Code { get; set; } = "";
    public string TitleFr { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public string? DescriptionFr { get; set; }
    public string? DescriptionEn { get; set; }
    public int DefaultSlaHours { get; set; } = 72;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}
