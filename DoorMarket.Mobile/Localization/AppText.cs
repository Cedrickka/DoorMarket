using System.Globalization;
using System.Resources;
using System.Reflection;

namespace DoorMarket.Mobile.Localization;

public static class AppText
{
    private static readonly ResourceManager ResourceManager = new(
        "DoorMarket.Mobile.Resources.Strings.AppResources",
        typeof(AppText).GetTypeInfo().Assembly);

    public static string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
