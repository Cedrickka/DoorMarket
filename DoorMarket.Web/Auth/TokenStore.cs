using Blazored.LocalStorage;
using Blazored.SessionStorage;
using Microsoft.JSInterop;

namespace DoorMarket.Web.Auth;

public class TokenStore
{
    private const string KeyToken = "dm_access_token";
    private const string KeyRemember = "dm_remember";

    private readonly ILocalStorageService _local;
    private readonly ISessionStorageService _session;

    private string? _cached;
    private bool? _rememberCached;

    public TokenStore(ILocalStorageService local, ISessionStorageService session)
    {
        _local = local;
        _session = session;
    }

    public async Task SetAsync(string token, bool rememberMe = true)
    {
        _cached = token;
        _rememberCached = rememberMe;

        await TryAsync(() => _local.SetItemAsync(KeyRemember, rememberMe));

        if (rememberMe)
        {
            await TryAsync(() => _local.SetItemAsync(KeyToken, token));
            await TryAsync(() => _session.RemoveItemAsync(KeyToken));
        }
        else
        {
            await TryAsync(() => _session.SetItemAsync(KeyToken, token));
            await TryAsync(() => _local.RemoveItemAsync(KeyToken));
        }
    }

    public async Task<string?> GetAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cached))
        {
            return _cached;
        }

        var remember = await GetRememberMeAsync();
        var token = remember
            ? await TryGetAsync<string?>(async () => await _local.GetItemAsync<string>(KeyToken))
            : await TryGetAsync<string?>(async () => await _session.GetItemAsync<string>(KeyToken));

        _cached = token;
        return token;
    }

    public async Task<bool> GetRememberMeAsync()
    {
        if (_rememberCached.HasValue)
        {
            return _rememberCached.Value;
        }

        var remember = await TryGetAsync(() => _local.GetItemAsync<bool?>(KeyRemember));
        _rememberCached = remember ?? true;
        return _rememberCached.Value;
    }

    public async Task ClearAsync()
    {
        _cached = null;
        _rememberCached = null;

        await TryAsync(() => _local.RemoveItemAsync(KeyToken));
        await TryAsync(() => _session.RemoveItemAsync(KeyToken));
        await TryAsync(() => _local.RemoveItemAsync(KeyRemember));
    }

    public string? Peek() => _cached;

    private static async Task TryAsync(Func<ValueTask> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (IsInteropUnavailable(ex))
        {
            // Ignore when JS runtime is unavailable (prerender/disconnect).
        }
    }

    private static async Task<T?> TryGetAsync<T>(Func<ValueTask<T?>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (IsInteropUnavailable(ex))
        {
            return default;
        }
    }

    private static bool IsInteropUnavailable(Exception ex)
    {
        if (ex is JSDisconnectedException)
        {
            return true;
        }

        if (ex is JSException)
        {
            return true;
        }

        if (ex is InvalidOperationException invalidOperation)
        {
            var message = invalidOperation.Message;
            if (message.Contains("JavaScript interop calls cannot be issued", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("statically rendered", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("prerender", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Operation is not valid due to the current state of the object", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return ex.InnerException is not null && IsInteropUnavailable(ex.InnerException);
    }
}
