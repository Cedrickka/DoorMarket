namespace DoorMarket.Api.Services;

public sealed class MarketingAutomationOptions
{
    public bool Enabled { get; set; } = true;
    public bool RunOnStartup { get; set; } = false;
    public int RunIntervalMinutes { get; set; } = 60;

    public bool BannerAutomationEnabled { get; set; } = true;
    public bool CampaignAutomationEnabled { get; set; } = true;
    public bool ScenarioAutomationEnabled { get; set; } = true;

    public bool IncludeDraftCampaigns { get; set; } = false;
    public int CampaignMaxPerRun { get; set; } = 20;
    public int ScenarioMaxPerRun { get; set; } = 20;
    public int AttributionWindowDays { get; set; } = 30;

    public bool IncludeWelcome { get; set; } = true;
    public bool IncludeWinback { get; set; } = true;
    public bool IncludeChurnRisk { get; set; } = true;
    public bool ScenarioSplitChannelsToCampaigns { get; set; } = false;

    public bool BannerDisableExpired { get; set; } = true;
    public bool BannerAutoPrioritizeLiveByCtr { get; set; } = false;
    public int BannerMinImpressionsForCtr { get; set; } = 100;

    public int AntiSpamStep1Hours { get; set; } = 24;
    public int AntiSpamStep2Hours { get; set; } = 48;
    public int AntiSpamStep3Hours { get; set; } = 72;
    public int ScenarioWindowDays { get; set; } = 7;
    public int ScenarioMaxTouchesPerUser { get; set; } = 3;
}
