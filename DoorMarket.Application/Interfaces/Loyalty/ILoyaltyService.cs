namespace DoorMarket.Application.Interfaces.Loyalty;

public interface ILoyaltyService
{
    Task AwardOrderPaidAsync(Guid orderId, CancellationToken ct);
}
