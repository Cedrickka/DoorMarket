using System.Globalization;
using DoorMarket.Mobile.Localization;
using Microsoft.Maui.Storage;

namespace DoorMarket.Mobile.Services;

public sealed class LanguageService
{
    private const string LanguagePreferenceKey = "dm_language";
    private const string DefaultLanguageCode = "fr";

    public string CurrentLanguageCode => NormalizeLanguageCode(
        Preferences.Default.Get(LanguagePreferenceKey, DefaultLanguageCode));

    public IReadOnlyList<LanguageOption> SupportedLanguages { get; } =
    [
        new("fr", "LanguageFrench"),
        new("en", "LanguageEnglish")
    ];

    public void Initialize()
    {
        ApplyCulture(CurrentLanguageCode);
    }

    public Task SetLanguageAsync(string languageCode)
    {
        var normalizedCode = NormalizeLanguageCode(languageCode);
        Preferences.Default.Set(LanguagePreferenceKey, normalizedCode);
        ApplyCulture(normalizedCode);
        LocalizationResourceManager.Instance.NotifyLanguageChanged();

        if (Microsoft.Maui.Controls.Application.Current is App app)
        {
            app.ReloadRootForLanguageChange();
        }

        return Task.CompletedTask;
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        return string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase)
            ? "en"
            : "fr";
    }

    private static void ApplyCulture(string languageCode)
    {
        var culture = new CultureInfo(languageCode == "en" ? "en-US" : "fr-FR");

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}

public sealed record LanguageOption(string Code, string ResourceKey);
