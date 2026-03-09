using System.ComponentModel;

namespace DoorMarket.Mobile.Localization;

public abstract class LocalizedContentPage : ContentPage
{
    private bool _isRefreshing;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LocalizationResourceManager.Instance.PropertyChanged += OnLocalizationChanged;
        OnLanguageChanged();
    }

    protected override void OnDisappearing()
    {
        LocalizationResourceManager.Instance.PropertyChanged -= OnLocalizationChanged;
        base.OnDisappearing();
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnLanguageChanged();
        _ = RefreshOnLanguageChangeAsync();
    }

    protected virtual void OnLanguageChanged()
    {
        OnPropertyChanged(string.Empty);
    }

    private async Task RefreshOnLanguageChangeAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        try
        {
            _isRefreshing = true;
            await RefreshLanguageDataAsync();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    protected virtual Task RefreshLanguageDataAsync()
    {
        return Task.CompletedTask;
    }
}
