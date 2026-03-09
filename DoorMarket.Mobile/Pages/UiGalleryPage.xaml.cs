using DoorMarket.Mobile.Localization;
using System.Collections.ObjectModel;

using DoorMarket.Mobile.Services;

namespace DoorMarket.Mobile.Pages;

public partial class UiGalleryPage : LocalizedContentPage
{
    public ObservableCollection<string> GalleryItems { get; } = new(["Tomates", "Bananes", "Avocats", "Fraises"]);

    private bool _isDarkMode;
    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (_isDarkMode == value)
            {
                return;
            }

            _isDarkMode = value;
            OnPropertyChanged();
        }
    }

    public UiGalleryPage()
    {
        InitializeComponent();
        IsDarkMode = ThemeManager.GetSavedTheme() == AppTheme.Dark;
        BindingContext = this;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await NavigationHelper.PopAsync();
    }

    private void OnThemeToggled(object sender, ToggledEventArgs e)
    {
        var theme = e.Value ? AppTheme.Dark : AppTheme.Light;
        IsDarkMode = e.Value;
        ThemeManager.SaveTheme(theme);
        ThemeManager.ApplyTheme(theme);
    }
}
