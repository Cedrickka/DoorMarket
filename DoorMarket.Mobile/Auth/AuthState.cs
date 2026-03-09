namespace DoorMarket.Mobile.Auth;

public sealed class AuthState
{
    public bool IsAuthenticated { get; private set; }

    public event EventHandler? Changed;

    internal void SetAuthenticated()
    {
        if (IsAuthenticated) return;
        IsAuthenticated = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void SetUnauthenticated()
    {
        if (!IsAuthenticated) return;
        IsAuthenticated = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
