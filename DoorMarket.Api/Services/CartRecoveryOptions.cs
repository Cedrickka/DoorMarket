namespace DoorMarket.Api.Services;

public sealed class CartRecoveryOptions
{
    public bool Enabled { get; set; } = true;
    public int RunIntervalMinutes { get; set; } = 15;
    public int StaleAfterMinutes { get; set; } = 180;
    public int AntiSpamWindowMinutes { get; set; } = 24 * 60;
    public int ConversionWindowHours { get; set; } = 24 * 7;
    public bool AbTestEnabled { get; set; } = true;
    public int HoldoutPercent { get; set; } = 20;
    public int ScanBatchSize { get; set; } = 200;
    public int MaxNotificationsPerRun { get; set; } = 50;
    public bool PushEnabled { get; set; } = false;
}
