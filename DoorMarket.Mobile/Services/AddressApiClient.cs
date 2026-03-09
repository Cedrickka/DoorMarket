using DoorMarket.Application.DTOs.Me;

namespace DoorMarket.Mobile.Services;

public sealed class AddressApiClient : ApiClientBase
{
    public AddressApiClient(HttpClient http) : base(http)
    {
    }

    public Task<List<AddressDto>> GetAddressesAsync(CancellationToken ct)
        => GetAsync<List<AddressDto>>("api/me/addresses", ct);

    public Task<AddressDto> CreateAsync(CreateAddressRequest request, CancellationToken ct)
        => PostAsync<AddressDto>("api/me/addresses", request, ct);

    public Task UpdateAsync(Guid id, UpdateAddressRequest request, CancellationToken ct)
        => PutAsync($"api/me/addresses/{id}", request, ct);

    public Task DeleteAsync(Guid id, CancellationToken ct)
        => base.DeleteAsync($"api/me/addresses/{id}", ct);
}
