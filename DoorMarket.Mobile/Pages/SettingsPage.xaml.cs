using DoorMarket.Mobile.Localization;
using DoorMarket.Mobile.Services;
using Microsoft.Maui.Storage;

namespace DoorMarket.Mobile.Pages;

public partial class SettingsPage : LocalizedContentPage
{
    private const string OrdersNotifKey = "dm_notifications_orders";
    private readonly LanguageService _languageService;
    private bool _isApplyingSelections;

    public SettingsPage(LanguageService languageService)
    {
        InitializeComponent();
        _languageService = languageService;
        _isApplyingSelections = true;

        ConfigurePickers();
        ThemePicker.SelectedIndexChanged += OnThemeChanged;
        LanguagePicker.SelectedIndexChanged += OnLanguageChanged;

        OrdersNotifSwitch.IsToggled = Preferences.Default.Get(OrdersNotifKey, true);
        OrdersNotifSwitch.Toggled += OnOrdersNotifToggled;

        VersionLabel.Text = $"DoorMarket Mobile {AppInfo.VersionString} ({AppInfo.BuildString})";
        _isApplyingSelections = false;
    }

    protected override Task RefreshLanguageDataAsync()
    {
        _isApplyingSelections = true;
        ConfigurePickers();
        _isApplyingSelections = false;
        return Task.CompletedTask;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_isApplyingSelections)
        {
            return;
        }

        var theme = ThemePicker.SelectedIndex switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };

        ThemeManager.SaveTheme(theme);
        ThemeManager.ApplyTheme(theme);
    }

    private async void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_isApplyingSelections || LanguagePicker.SelectedIndex < 0)
        {
            return;
        }

        var code = LanguagePicker.SelectedIndex == 1 ? "en" : "fr";
        await _languageService.SetLanguageAsync(code);
    }

    private async void OnLanguageTapped(object? sender, TappedEventArgs e)
    {
        var french = $"🇫🇷 {AppText.Get("LanguageFrench")}";
        var english = $"🇺🇸 {AppText.Get("LanguageEnglish")}";
        var selected = await DisplayActionSheet(AppText.Get("SettingsLanguage"), AppText.Get("Cancel"), null, french, english);

        if (selected == french)
        {
            await _languageService.SetLanguageAsync("fr");
        }
        else if (selected == english)
        {
            await _languageService.SetLanguageAsync("en");
        }
    }

    private void OnOrdersNotifToggled(object? sender, ToggledEventArgs e)
    {
        Preferences.Default.Set(OrdersNotifKey, e.Value);
    }

    private static int ThemeToIndex(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Light => 1,
            AppTheme.Dark => 2,
            _ => 0
        };
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await NavigationHelper.PopAsync();
    }

    private void SetCurrentLanguageImage()
    {
        CurrentLanguageImage.Source = _languageService.CurrentLanguageCode == "en"
            ? "https://flagcdn.com/w20/us.png"
            : "https://flagcdn.com/w20/fr.png";
    }

    private void ConfigurePickers()
    {
        ThemePicker.ItemsSource = new List<string>
        {
            AppText.Get("ThemeSystem"),
            AppText.Get("ThemeLight"),
            AppText.Get("ThemeDark")
        };
        ThemePicker.SelectedIndex = ThemeToIndex(ThemeManager.GetSavedTheme());

        LanguagePicker.ItemsSource = _languageService.SupportedLanguages
            .Select(x => AppText.Get(x.ResourceKey))
            .ToList();
        LanguagePicker.SelectedIndex = _languageService.CurrentLanguageCode == "en" ? 1 : 0;
        SetCurrentLanguageImage();
    }
}
