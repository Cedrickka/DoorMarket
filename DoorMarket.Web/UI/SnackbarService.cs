namespace DoorMarket.Web.UI;

public sealed class SnackbarService
{
    public event Action<string, string>? OnShow; // (message, type)

    public void Success(string msg) => OnShow?.Invoke(msg, "ok");
    public void Error(string msg) => OnShow?.Invoke(msg, "err");
    public void Info(string msg) => OnShow?.Invoke(msg, "info");
}
