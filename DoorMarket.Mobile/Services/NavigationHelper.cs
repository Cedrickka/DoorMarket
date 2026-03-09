using Microsoft.Maui.Controls;

namespace DoorMarket.Mobile.Services;

public static class NavigationHelper
{
    public static async Task PushAsync(Page page)
    {
        if (Shell.Current is not null)
        {
            try
            {
                await Shell.Current.Navigation.PushAsync(page);
                return;
            }
            catch
            {
                // Fall back to root navigation if shell stack is unavailable.
            }
        }

        var nav = Microsoft.Maui.Controls.Application.Current?.MainPage?.Navigation;
        if (nav is not null)
        {
            await nav.PushAsync(page);
        }
    }

    public static async Task PopAsync()
    {
        if (Shell.Current is not null)
        {
            var shellNav = Shell.Current.Navigation;
            if (shellNav.NavigationStack.Count > 1)
            {
                await shellNav.PopAsync();
                return;
            }
        }

        var nav = Microsoft.Maui.Controls.Application.Current?.MainPage?.Navigation;
        if (nav is not null && nav.NavigationStack.Count > 1)
        {
            await nav.PopAsync();
        }
    }
}
