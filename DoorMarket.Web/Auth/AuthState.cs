namespace DoorMarket.Web.Auth;

public class AuthState
{
    public bool IsReady { get; private set; } = true;
    public bool IsAuthenticated { get; private set; }
    public string? Role { get; private set; }
    public string? Email { get; private set; }

    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Role, "3", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Role, "4", StringComparison.OrdinalIgnoreCase);
    public bool IsSuperAdmin => string.Equals(Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Role, "4", StringComparison.OrdinalIgnoreCase);
    public bool IsShop => string.Equals(Role, "Shop", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Role, "2", StringComparison.OrdinalIgnoreCase);
    public bool IsClient => string.Equals(Role, "Client", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Role, "1", StringComparison.OrdinalIgnoreCase);

    public event Action? OnChange;

    private void Notify() => OnChange?.Invoke();

    public void SetNotReady()
    {
        IsReady = false;
        Notify();
    }

    public void SetAuthenticated(string? role, string? email)
    {
        IsAuthenticated = true;
        Role = role;
        Email = email;
        IsReady = true;
        Notify();
    }

    public void SetUnauthenticated()
    {
        IsAuthenticated = false;
        Role = null;
        Email = null;
        IsReady = true;
        Notify();
    }
}
