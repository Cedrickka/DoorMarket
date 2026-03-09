namespace DoorMarket.Mobile.Services;

public sealed class SupportApiClient : ApiClientBase
{
    public SupportApiClient(HttpClient http) : base(http)
    {
    }

    public Task SendContactAsync(SupportContactRequest request, CancellationToken ct)
        => PostAsync("api/support/contact", request, ct);

    public sealed record SupportContactRequest(string Name, string Email, string Subject, string Message);
}
