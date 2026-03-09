using DoorMarket.Application.DTOs.Cart;

namespace DoorMarket.Application.Interfaces.Cart;

public interface ICartService
{
    Task<CartDto> GetMyCartAsync(CancellationToken ct);
    Task<CartDto> AddItemAsync(Guid productId, int qty, CancellationToken ct);
    Task<CartDto> UpdateItemAsync(Guid cartItemId, int qty, CancellationToken ct);
    Task<CartDto> RemoveItemAsync(Guid cartItemId, CancellationToken ct);
    Task ClearAsync(CancellationToken ct);
}
