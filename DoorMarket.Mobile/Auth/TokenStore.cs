using System.Globalization;
using DoorMarket.Application.DTOs.Auth;
using Microsoft.Maui.Storage;

namespace DoorMarket.Mobile.Auth;

public sealed class TokenStore
{
    private const string KeyAccess = "dm_access_token";
    private const string KeyRefresh = "dm_refresh_token";
    private const string KeyExpires = "dm_access_expires";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _accessCache;
    private string? _refreshCache;
    private DateTime? _expiresCache;

    public async Task SetTokensAsync(AuthResponse response)
    {
        await _lock.WaitAsync();
        try
        {
            _accessCache = response.AccessToken;
            _refreshCache = response.RefreshToken;
            _expiresCache = response.ExpiresAtUtc;

            await SetSecureAsync(KeyAccess, response.AccessToken);
            await SetSecureAsync(KeyRefresh, response.RefreshToken);
            await SetSecureAsync(KeyExpires, response.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        if (!string.IsNullOrWhiteSpace(_accessCache))
            return _accessCache;

        _accessCache = await GetSecureAsync(KeyAccess);
        return _accessCache;
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        if (!string.IsNullOrWhiteSpace(_refreshCache))
            return _refreshCache;

        _refreshCache = await GetSecureAsync(KeyRefresh);
        return _refreshCache;
    }

    public async Task<DateTime?> GetExpiresAtUtcAsync()
    {
        if (_expiresCache.HasValue)
            return _expiresCache.Value;

        var raw = await GetSecureAsync(KeyExpires);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
        {
            _expiresCache = dt;
            return dt;
        }

        return null;
    }

    public async Task<bool> IsAccessTokenExpiredAsync(TimeSpan? skew = null)
    {
        var expiresAt = await GetExpiresAtUtcAsync();
        if (!expiresAt.HasValue)
            return true;

        var buffer = skew ?? TimeSpan.FromSeconds(30);
        return DateTime.UtcNow >= expiresAt.Value.Subtract(buffer);
    }

    public async Task ClearAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _accessCache = null;
            _refreshCache = null;
            _expiresCache = null;

            await SetSecureAsync(KeyAccess, null);
            await SetSecureAsync(KeyRefresh, null);
            await SetSecureAsync(KeyExpires, null);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task<string?> GetSecureAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch
        {
            return Preferences.Default.Get<string?>(key, null);
        }
    }

    private static async Task SetSecureAsync(string key, string? value)
    {
        try
        {
            if (value == null)
            {
                SecureStorage.Default.Remove(key);
            }
            else
            {
                await SecureStorage.Default.SetAsync(key, value);
            }
        }
        catch
        {
            if (value == null)
            {
                Preferences.Default.Remove(key);
            }
            else
            {
                Preferences.Default.Set(key, value);
            }
        }
    }
}
