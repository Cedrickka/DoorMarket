namespace DoorMarket.Api.Services;

public sealed class CheckoutAlertingOptions
{
    public bool Enabled { get; set; } = true;
    public bool RunOnStartup { get; set; } = true;
    public int RunIntervalMinutes { get; set; } = 5;
    public int AnalysisWindowMinutes { get; set; } = 60;
    public int NotifyCooldownMinutes { get; set; } = 30;
    public bool NotifyByEmail { get; set; } = true;
    public bool NotifyBySlack { get; set; } = false;
    public string? AlertEmails { get; set; }
    public string? SlackWebhookUrl { get; set; }
    public string? SlackMention { get; set; }
    public int MaxIncidentsPerRun { get; set; } = 30;

    public decimal PaymentSuccessCriticalThresholdPct { get; set; } = 65m;
    public decimal PaymentSuccessWarningThresholdPct { get; set; } = 80m;
    public int PaymentSuccessCriticalMinInitiatedCount { get; set; } = 20;
    public int PaymentSuccessWarningMinInitiatedCount { get; set; } = 10;

    public decimal SubmitToOrderWarningThresholdPct { get; set; } = 60m;
    public int SubmitToOrderWarningMinSubmitCount { get; set; } = 20;

    public int PaymentLatencyP95WarningMs { get; set; } = 12000;
    public int RedirectFailuresWarningCount { get; set; } = 5;

    public decimal ProviderFailureCriticalThresholdPct { get; set; } = 40m;
    public decimal ProviderFailureWarningThresholdPct { get; set; } = 25m;
    public int ProviderFailureMinInitiatedCount { get; set; } = 8;
}
