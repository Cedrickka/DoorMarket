using System.ComponentModel;

namespace DoorMarket.Mobile.Localization;

public sealed class LocalizationResourceManager : INotifyPropertyChanged
{
    public static LocalizationResourceManager Instance { get; } = new();

    private LocalizationResourceManager()
    {
    }

    public string this[string key] => AppText.Get(key);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyLanguageChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
