using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DoorMarket.Application.DTOs.Auth;
using DoorMarket.Application.DTOs.Me;
using DoorMarket.Domain.Enums;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Localization;
using DoorMarket.Mobile.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Maui.ApplicationModel;

namespace DoorMarket.Mobile.Auth;

public sealed class AuthSession
{
    private readonly AuthApiClient _authApi;
    private readonly TokenStore _store;
    private readonly AuthState _state;
    private readonly IHttpClientFactory _httpFactory;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthSession(AuthApiClient authApi, TokenStore store, AuthState state, IHttpClientFactory httpFactory)
    {
        _authApi = authApi;
        _store = store;
        _state = state;
        _httpFactory = httpFactory;
    }

    public async Task<bool> InitializeAsync(CancellationToken ct = default)
    {
        var access = await _store.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(access))
        {
            _state.SetUnauthenticated();
            return false;
        }

        var expired = await _store.IsAccessTokenExpiredAsync();
        if (!expired)
        {
            if (!await EnsureRoleMatchesAsync(access, ct))
            {
                return false;
            }

            _state.SetAuthenticated();
            return true;
        }

        return await TryRefreshAsync(ct);
    }

    public async Task SignInAsync(AuthResponse response)
    {
        await _store.SetTokensAsync(response);
        if (!await EnsureRoleMatchesAsync(response.AccessToken, CancellationToken.None))
        {
            await _store.ClearAsync();
            _state.SetUnauthenticated();
            return;
        }

        _state.SetAuthenticated();
    }

    public Task SignInAsync(LoginResult response)
    {
        var tokens = new AuthResponse(response.AccessToken, response.RefreshToken, response.ExpiresAtUtc);
        return SignInAsync(tokens);
    }

    public async Task<bool> TryRefreshAsync(CancellationToken ct = default)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            var access = await _store.GetAccessTokenAsync();
            if (!string.IsNullOrWhiteSpace(access) && !await _store.IsAccessTokenExpiredAsync())
            {
                _state.SetAuthenticated();
                return true;
            }

            var refresh = await _store.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refresh))
            {
                await _store.ClearAsync();
                _state.SetUnauthenticated();
                return false;
            }

            var response = await _authApi.RefreshAsync(new RefreshRequest(refresh), ct);
            await _store.SetTokensAsync(response);

            if (!await EnsureRoleMatchesAsync(response.AccessToken, ct))
            {
                await _store.ClearAsync();
                _state.SetUnauthenticated();
                return false;
            }

            _state.SetAuthenticated();
            return true;
        }
        catch
        {
            await _store.ClearAsync();
            _state.SetUnauthenticated();
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task SignOutAsync(CancellationToken ct = default)
    {
        var refresh = await _store.GetRefreshTokenAsync();
        if (!string.IsNullOrWhiteSpace(refresh))
        {
            try
            {
                await _authApi.LogoutAsync(new RefreshRequest(refresh), ct);
            }
            catch
            {
            }
        }

        await _store.ClearAsync();
        _state.SetUnauthenticated();
    }

    private async Task<bool> EnsureRoleMatchesAsync(string? accessToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        var tokenRole = ReadRoleFromToken(accessToken);
        if (string.IsNullOrWhiteSpace(tokenRole))
        {
            return true;
        }

        try
        {
            var me = await FetchMeAsync(accessToken, ct);
            if (me is null)
            {
                return true;
            }

            var normalizedTokenRole = NormalizeRole(tokenRole);
            var normalizedDbRole = NormalizeRole(me.Role);
            if (!string.Equals(normalizedTokenRole, normalizedDbRole, StringComparison.OrdinalIgnoreCase))
            {
                await HandleRoleMismatchAsync(ct);
                return false;
            }

            return true;
        }
        catch (ApiException ex) when (ex.StatusCode is 401 or 403)
        {
            await _store.ClearAsync();
            _state.SetUnauthenticated();
            return false;
        }
        catch
        {
            // On laisse passer si erreur réseau/transitoire.
            return true;
        }
    }

    private async Task<MeDto?> FetchMeAsync(string accessToken, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("Auth");
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ApiException("Unauthorized", (int)response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MeDto>(cancellationToken: ct);
    }

    private async Task HandleRoleMismatchAsync(CancellationToken ct)
    {
        var signOutFlag = 0;

        async Task SignOutOnceAsync()
        {
            if (Interlocked.Exchange(ref signOutFlag, 1) == 1)
            {
                return;
            }

            await SignOutAsync(ct);
        }

        await ShowRoleChangedToastAsync();

        _ = ShowReconnectDialogAsync(SignOutOnceAsync);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2.5), ct);
        }
        catch
        {
        }

        await SignOutOnceAsync();
    }

    private static async Task ShowRoleChangedToastAsync()
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    var toast = Toast.Make(AppText.Get("RoleChangedMessage"), ToastDuration.Short);
                    _ = toast.Show();
                }
                catch
                {
                }
            });

        }
        catch
        {
        }
    }

    private static async Task ShowReconnectDialogAsync(Func<Task> onReconnect)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var page = Microsoft.Maui.Controls.Application.Current?.MainPage;
                if (page is null)
                {
                    return;
                }

                try
                {
                    var title = AppText.Get("RoleChangedTitle");
                    var message = AppText.Get("RoleChangedMessage");
                    var reconnect = AppText.Get("RoleChangedReconnect");
                    var later = AppText.Get("RoleChangedLater");

                    var accepted = await page.DisplayAlert(title, message, reconnect, later);
                    if (accepted)
                    {
                        await onReconnect();
                    }
                }
                catch
                {
                }
            });
        }
        catch
        {
        }
    }

    private static string? ReadRoleFromToken(string accessToken)
    {
        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length < 2)
            {
                return null;
            }

            var json = DecodeBase64Url(parts[1]);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("role", out var roleNode) && roleNode.ValueKind == JsonValueKind.String)
            {
                return roleNode.GetString();
            }

            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name.EndsWith("/role", StringComparison.OrdinalIgnoreCase) &&
                    prop.Value.ValueKind == JsonValueKind.String)
                {
                    return prop.Value.GetString();
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? DecodeBase64Url(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        try
        {
            var bytes = Convert.FromBase64String(padded);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeRole(string? role)
    {
        var value = role?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (int.TryParse(value, out var roleId) && Enum.IsDefined(typeof(UserRole), roleId))
        {
            return ((UserRole)roleId).ToString();
        }

        return value;
    }
}
