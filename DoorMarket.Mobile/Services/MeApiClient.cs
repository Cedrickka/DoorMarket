using DoorMarket.Application.DTOs.Me;
using DoorMarket.Mobile.Http;

namespace DoorMarket.Mobile.Services;

public sealed class MeApiClient : ApiClientBase
{
    public MeApiClient(HttpClient http) : base(http)
    {
    }

    public Task<MeDto> GetMeAsync(CancellationToken ct)
        => GetAsync<MeDto>("api/me", ct);

    public async Task<MeDto> GetMeNoRefreshAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/me");
        request.Options.Set(AuthMessageHandler.SkipRefreshKey, true);
        using var response = await Http.SendAsync(request, ct);
        return await ReadAsync<MeDto>(response, ct);
    }

    public Task UpdateMeAsync(UpdateMeRequest request, CancellationToken ct)
        => PutAsync("api/me", request, ct);

    public Task RequestPhoneChangeAsync(RequestPhoneChangeRequest request, CancellationToken ct)
        => PostAsync("api/me/request-phone-change", request, ct);

    public Task ConfirmPhoneChangeAsync(ConfirmPhoneChangeRequest request, CancellationToken ct)
        => PostAsync("api/me/confirm-phone-change", request, ct);

    public Task RequestPasswordChangeAsync(RequestPasswordChangeRequest request, CancellationToken ct)
        => PostAsync("api/me/request-password-change", request, ct);

    public Task ConfirmPasswordChangeAsync(ConfirmPasswordChangeRequest request, CancellationToken ct)
        => PostAsync("api/me/confirm-password-change", request, ct);

    public async Task<ProfileImageUploadResponse> UploadProfilePhotoAsync(Stream content, string fileName, string contentType, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(streamContent, "file", fileName);

        return await PostMultipartAsync<ProfileImageUploadResponse>("api/me/upload-photo", form, ct);
    }
}
