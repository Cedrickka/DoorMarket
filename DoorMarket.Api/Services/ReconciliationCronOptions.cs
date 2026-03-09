namespace DoorMarket.Api.Services;

public sealed class ReconciliationCronOptions
{
    public bool Enabled { get; set; } = true;
    public bool RunOnStartup { get; set; } = false;
    public int RunIntervalMinutes { get; set; } = 30;
    public int LockStaleAfterMinutes { get; set; } = 120;
    public int WindowDays { get; set; } = 90;
    public int DraftSlaDays { get; set; } = 7;
    public int ApprovedSlaDays { get; set; } = 3;
    public int MaxPayoutsScan { get; set; } = 15000;
}
