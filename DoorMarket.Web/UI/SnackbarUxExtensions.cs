using MudBlazor;

namespace DoorMarket.Web.UI;

public static class SnackbarUxExtensions
{
    public static void ShowSuccess(this ISnackbar snackbar, string message)
        => snackbar.Add(message, Severity.Success, Configure);

    public static void ShowInfo(this ISnackbar snackbar, string message)
        => snackbar.Add(message, Severity.Info, Configure);

    public static void ShowWarning(this ISnackbar snackbar, string message)
        => snackbar.Add(message, Severity.Warning, Configure);

    public static void ShowError(this ISnackbar snackbar, string message)
        => snackbar.Add(message, Severity.Error, Configure);

    private static void Configure(SnackbarOptions options)
    {
        options.ShowCloseIcon = true;
        options.CloseAfterNavigation = true;
        options.VisibleStateDuration = 4500;
        options.ShowTransitionDuration = 150;
        options.HideTransitionDuration = 150;
    }
}
