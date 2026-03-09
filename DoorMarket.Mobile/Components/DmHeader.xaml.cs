using DoorMarket.Mobile.Localization;
using DoorMarket.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Input;

namespace DoorMarket.Mobile.Components;

public partial class DmHeader : ContentView
{
    private LanguageService? _languageService;

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title),
        typeof(string),
        typeof(DmHeader),
        string.Empty,
        propertyChanged: OnTitleChanged);

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        nameof(Subtitle),
        typeof(string),
        typeof(DmHeader),
        string.Empty,
        propertyChanged: OnSubtitleChanged);

    public static readonly BindableProperty ActionGlyphProperty = BindableProperty.Create(
        nameof(ActionGlyph),
        typeof(string),
        typeof(DmHeader),
        string.Empty,
        propertyChanged: OnActionChanged);

    public static readonly BindableProperty ActionFontFamilyProperty = BindableProperty.Create(
        nameof(ActionFontFamily),
        typeof(string),
        typeof(DmHeader),
        "fa-solid-900");

    public static readonly BindableProperty BackGlyphProperty = BindableProperty.Create(
        nameof(BackGlyph),
        typeof(string),
        typeof(DmHeader),
        "\uf053");

    public static readonly BindableProperty BackFontFamilyProperty = BindableProperty.Create(
        nameof(BackFontFamily),
        typeof(string),
        typeof(DmHeader),
        "fa-solid-900");

    public static readonly BindableProperty ActionCommandProperty = BindableProperty.Create(
        nameof(ActionCommand),
        typeof(ICommand),
        typeof(DmHeader));

    public static readonly BindableProperty ActionCommandParameterProperty = BindableProperty.Create(
        nameof(ActionCommandParameter),
        typeof(object),
        typeof(DmHeader));

    public static readonly BindableProperty HasActionProperty = BindableProperty.Create(
        nameof(HasAction),
        typeof(bool),
        typeof(DmHeader),
        false);

    public static readonly BindableProperty HasBackProperty = BindableProperty.Create(
        nameof(HasBack),
        typeof(bool),
        typeof(DmHeader),
        false);

    public static readonly BindableProperty BackCommandProperty = BindableProperty.Create(
        nameof(BackCommand),
        typeof(ICommand),
        typeof(DmHeader));

    public static readonly BindableProperty HasSubtitleProperty = BindableProperty.Create(
        nameof(HasSubtitle),
        typeof(bool),
        typeof(DmHeader),
        false);

    public static readonly BindableProperty HasTitleProperty = BindableProperty.Create(
        nameof(HasTitle),
        typeof(bool),
        typeof(DmHeader),
        false);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string ActionGlyph
    {
        get => (string)GetValue(ActionGlyphProperty);
        set => SetValue(ActionGlyphProperty, value);
    }

    public string ActionFontFamily
    {
        get => (string)GetValue(ActionFontFamilyProperty);
        set => SetValue(ActionFontFamilyProperty, value);
    }

    public string BackGlyph
    {
        get => (string)GetValue(BackGlyphProperty);
        set => SetValue(BackGlyphProperty, value);
    }

    public string BackFontFamily
    {
        get => (string)GetValue(BackFontFamilyProperty);
        set => SetValue(BackFontFamilyProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public object? ActionCommandParameter
    {
        get => GetValue(ActionCommandParameterProperty);
        set => SetValue(ActionCommandParameterProperty, value);
    }

    public bool HasAction
    {
        get => (bool)GetValue(HasActionProperty);
        private set => SetValue(HasActionProperty, value);
    }

    public bool HasBack
    {
        get => (bool)GetValue(HasBackProperty);
        set => SetValue(HasBackProperty, value);
    }

    public ICommand? BackCommand
    {
        get => (ICommand?)GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }

    public bool HasSubtitle
    {
        get => (bool)GetValue(HasSubtitleProperty);
        private set => SetValue(HasSubtitleProperty, value);
    }

    public bool HasTitle
    {
        get => (bool)GetValue(HasTitleProperty);
        private set => SetValue(HasTitleProperty, value);
    }

    public string CurrentLanguageFlagImage =>
        (_languageService?.CurrentLanguageCode ?? "fr") == "en"
            ? "https://flagcdn.com/w20/us.png"
            : "https://flagcdn.com/w20/fr.png";

    public DmHeader()
    {
        InitializeComponent();
        HasTitle = !string.IsNullOrWhiteSpace(Title);
        HasSubtitle = !string.IsNullOrWhiteSpace(Subtitle);
        HasAction = !string.IsNullOrWhiteSpace(ActionGlyph);
        Loaded += OnLoaded;
    }

    private static void OnActionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DmHeader header)
        {
            header.HasAction = !string.IsNullOrWhiteSpace(newValue as string);
        }
    }

    private static void OnTitleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DmHeader header)
        {
            header.HasTitle = !string.IsNullOrWhiteSpace(newValue as string);
        }
    }

    private static void OnSubtitleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DmHeader header)
        {
            header.HasSubtitle = !string.IsNullOrWhiteSpace(newValue as string);
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _languageService ??= Handler?.MauiContext?.Services.GetService<LanguageService>();
        OnPropertyChanged(nameof(CurrentLanguageFlagImage));
    }

    private async void OnLanguageTapped(object? sender, TappedEventArgs e)
    {
        _languageService ??= Handler?.MauiContext?.Services.GetService<LanguageService>();
        if (_languageService is null)
        {
            return;
        }

        var page = FindParentPage();
        if (page is null)
        {
            return;
        }

        var french = $"🇫🇷 {AppText.Get("LanguageFrench")}";
        var english = $"🇺🇸 {AppText.Get("LanguageEnglish")}";
        var cancel = AppText.Get("Cancel");

        var selected = await page.DisplayActionSheet(
            AppText.Get("SettingsLanguage"),
            cancel,
            null,
            french,
            english);

        if (selected == french)
        {
            await _languageService.SetLanguageAsync("fr");
        }
        else if (selected == english)
        {
            await _languageService.SetLanguageAsync("en");
        }
    }

    private Page? FindParentPage()
    {
        Element? element = this;
        while (element is not null && element is not Page)
        {
            element = element.Parent;
        }

        return element as Page;
    }
}
