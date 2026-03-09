using DoorMarket.Application.DTOs.Cart;

namespace DoorMarket.Application.Interfaces.Cart;

public interface IPromoService
{
    PromoQuoteDto Evaluate(string? code, decimal subtotal);
}
