using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using DoorMarket.Mobile.Http;

namespace DoorMarket.Mobile.Services;

public abstract class ApiClientBase
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan TransientRetryDelay = TimeSpan.FromMilliseconds(350);
    private const string TimeoutMessage = "Le serveur met trop de temps a repondre. Verifiez votre connexion et l'adresse API.";
    private const string NetworkMessage = "Impossible de joindre le serveur DoorMarket.";
    private const string RemoteResetMessage = "Connexion interrompue par le serveur distant. Verifiez que l'API est demarree et accessible sur le reseau.";

    protected ApiClientBase(HttpClient http)
    {
        Http = http;
    }

    protected HttpClient Http { get; }

    protected Task<T> GetAsync<T>(string path, CancellationToken ct)
        => SendAndReadAsync<T>(token => Http.GetAsync(path, token), ct, allowRetry: true);

    protected Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
        => SendAndReadAsync<T>(token => Http.PostAsJsonAsync(path, body, token), ct);

    protected Task PostAsync(string path, object body, CancellationToken ct)
        => SendAsync(token => Http.PostAsJsonAsync(path, body, token), ct);

    protected Task PutAsync(string path, object body, CancellationToken ct)
        => SendAsync(token => Http.PutAsJsonAsync(path, body, token), ct);

    protected Task<T> PutAsync<T>(string path, object body, CancellationToken ct)
        => SendAndReadAsync<T>(token => Http.PutAsJsonAsync(path, body, token), ct);

    protected Task DeleteAsync(string path, CancellationToken ct)
        => SendAsync(token => Http.DeleteAsync(path, token), ct);

    protected Task<T> DeleteAsync<T>(string path, CancellationToken ct)
        => SendAndReadAsync<T>(token => Http.DeleteAsync(path, token), ct);

    protected Task<T> PostMultipartAsync<T>(string path, MultipartFormDataContent content, CancellationToken ct)
        => SendAndReadAsync<T>(token => Http.PostAsync(path, content, token), ct);

    private async Task SendAsync(Func<CancellationToken, Task<HttpResponseMessage>> sender, CancellationToken ct)
    {
        using var timeoutCts = BuildTimeoutToken(ct);
        try
        {
            var response = await sender(timeoutCts.Token);
            await EnsureSuccess(response, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ApiException(TimeoutMessage, 408);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(BuildNetworkMessage(ex), 503);
        }
        catch (IOException ex)
        {
            throw new ApiException(BuildIoMessage(ex), 503);
        }
    }

    private async Task<T> SendAndReadAsync<T>(
        Func<CancellationToken, Task<HttpResponseMessage>> sender,
        CancellationToken ct,
        bool allowRetry = false)
    {
        using var timeoutCts = BuildTimeoutToken(ct);
        try
        {
            var response = await sender(timeoutCts.Token);
            return await ReadAsync<T>(response, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            if (allowRetry && await TryDelayAsync(ct))
            {
                return await SendAndReadAsync<T>(sender, ct, allowRetry: false);
            }

            throw new ApiException(TimeoutMessage, 408);
        }
        catch (HttpRequestException ex)
        {
            if (allowRetry && IsTransientNetworkError(ex) && await TryDelayAsync(ct))
            {
                return await SendAndReadAsync<T>(sender, ct, allowRetry: false);
            }

            throw new ApiException(BuildNetworkMessage(ex), 503);
        }
        catch (IOException ex)
        {
            if (allowRetry && IsTransientNetworkError(ex) && await TryDelayAsync(ct))
            {
                return await SendAndReadAsync<T>(sender, ct, allowRetry: false);
            }

            throw new ApiException(BuildIoMessage(ex), 503);
        }
    }

    private static CancellationTokenSource BuildTimeoutToken(CancellationToken ct)
    {
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(RequestTimeout);
        return timeoutCts;
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            if (data is null)
            {
                throw new ApiException("Reponse serveur vide.", (int)response.StatusCode);
            }

            return data;
        }

        var text = await response.Content.ReadAsStringAsync(ct);
        var parsed = ParseError(text, response.ReasonPhrase);
        throw new ApiException(parsed.message, (int)response.StatusCode, parsed.code);
    }

    protected static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var text = await response.Content.ReadAsStringAsync(ct);
        var parsed = ParseError(text, response.ReasonPhrase);
        throw new ApiException(parsed.message, (int)response.StatusCode, parsed.code);
    }

    private static string BuildNetworkMessage(HttpRequestException ex)
    {
        if (ex.InnerException is SocketException socketException)
        {
            if (socketException.SocketErrorCode == SocketError.ConnectionReset)
            {
                return RemoteResetMessage;
            }

            return $"{NetworkMessage} Le reseau a coupe la connexion distante.";
        }

        var msg = ex.Message ?? string.Empty;
        if (LooksLikeRemoteReset(msg))
        {
            return RemoteResetMessage;
        }

        return $"{NetworkMessage} {msg}";
    }

    private static string BuildIoMessage(IOException ex)
    {
        var msg = ex.Message ?? string.Empty;
        if (LooksLikeRemoteReset(msg))
        {
            return RemoteResetMessage;
        }

        return string.IsNullOrWhiteSpace(msg)
            ? NetworkMessage
            : $"{NetworkMessage} {msg}";
    }

    private static bool LooksLikeRemoteReset(string message)
        => message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase)
           || message.Contains("connection reset", StringComparison.OrdinalIgnoreCase)
           || message.Contains("copying content to a stream", StringComparison.OrdinalIgnoreCase)
           || message.Contains("broken pipe", StringComparison.OrdinalIgnoreCase);

    private static bool IsTransientNetworkError(Exception ex)
        => ex is HttpRequestException || ex is IOException;

    private static async Task<bool> TryDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TransientRetryDelay, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static (string message, string? code) ParseError(string? text, string? fallbackReason)
    {
        var fallback = string.IsNullOrWhiteSpace(text)
            ? fallbackReason ?? "Erreur serveur."
            : text;

        if (string.IsNullOrWhiteSpace(text))
        {
            return (fallback, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var message = root.TryGetProperty("error", out var errorNode) && errorNode.ValueKind == JsonValueKind.String
                ? errorNode.GetString()
                : null;
            var code = root.TryGetProperty("code", out var codeNode) && codeNode.ValueKind == JsonValueKind.String
                ? codeNode.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(message) &&
                root.TryGetProperty("title", out var titleNode) &&
                titleNode.ValueKind == JsonValueKind.String)
            {
                message = titleNode.GetString();
            }

            if (root.TryGetProperty("errors", out var errorsNode) &&
                errorsNode.ValueKind == JsonValueKind.Object)
            {
                var first = errorsNode.EnumerateObject().FirstOrDefault();
                if (first.Value.ValueKind == JsonValueKind.Array)
                {
                    var firstMsg = first.Value.EnumerateArray().FirstOrDefault();
                    if (firstMsg.ValueKind == JsonValueKind.String)
                    {
                        message = firstMsg.GetString();
                    }
                }
            }

            return (string.IsNullOrWhiteSpace(message) ? fallback : message!, code);
        }
        catch
        {
            return (fallback, null);
        }
    }
}
