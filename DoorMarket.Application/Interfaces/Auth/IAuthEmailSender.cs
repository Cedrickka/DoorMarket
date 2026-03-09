namespace DoorMarket.Application.Interfaces.Auth;

public interface IAuthEmailSender
{
    Task SendVerificationCodeAsync(string email, string code, CancellationToken ct);
}
