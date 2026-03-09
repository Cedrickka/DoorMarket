using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace DoorMarket.Mobile.Services;

public static class ThemeManager
{
    private const string ThemeKey = "dm_theme";

    public static void ApplySavedTheme()
    {
        ApplyTheme(GetSavedTheme());
    }

    public static void ApplyTheme(AppTheme theme)
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app != null)
        {
            app.UserAppTheme = theme;
            ApplySemanticPalette(app.Resources, theme == AppTheme.Dark);
        }
    }

    public static AppTheme GetSavedTheme()
    {
        var raw = Preferences.Default.Get(ThemeKey, "System");
        return raw switch
        {
            "Light" => AppTheme.Light,
            "Dark" => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }

    public static void SaveTheme(AppTheme theme)
    {
        var raw = theme switch
        {
            AppTheme.Light => "Light",
            AppTheme.Dark => "Dark",
            _ => "System"
        };

        Preferences.Default.Set(ThemeKey, raw);
    }

    private static void ApplySemanticPalette(ResourceDictionary resources, bool darkMode)
    {
        ApplyColor(resources, "DmBg", darkMode ? "DmBgDark" : "DmBgLight");
        ApplyColor(resources, "DmSurface", darkMode ? "DmSurfaceDark" : "DmSurfaceLight");
        ApplyColor(resources, "DmSurfaceAlt", darkMode ? "DmSurfaceAltDark" : "DmSurfaceAltLight");
        ApplyColor(resources, "DmTextPrimary", darkMode ? "DmTextPrimaryDark" : "DmTextPrimaryLight");
        ApplyColor(resources, "DmTextSecondary", darkMode ? "DmTextSecondaryDark" : "DmTextSecondaryLight");
        ApplyColor(resources, "DmBorder", darkMode ? "DmBorderDark" : "DmBorderLight");
        ApplyColor(resources, "DmInputBg", darkMode ? "DmInputBgDark" : "DmInputBgLight");
        ApplyColor(resources, "DmShadow", darkMode ? "DmShadowDark" : "DmShadowLight");
        ApplyColor(resources, "DmHeaderBg", darkMode ? "DmHeaderBgDark" : "DmHeaderBgLight");
        ApplyColor(resources, "DmHeaderText", darkMode ? "DmHeaderTextDark" : "DmHeaderTextLight");
        ApplyColor(resources, "DmChipBg", darkMode ? "DmChipBgDark" : "DmChipBgLight");
    }

    private static void ApplyColor(ResourceDictionary resources, string semanticKey, string sourceKey)
    {
        if (resources.TryGetValue(sourceKey, out var value) && value is Color color)
        {
            resources[semanticKey] = color;
        }
    }
}
