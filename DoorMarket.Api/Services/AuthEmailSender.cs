using DoorMarket.Application.Interfaces.Auth;
using Microsoft.AspNetCore.Http;

namespace DoorMarket.Api.Services;

public sealed class AuthEmailSender : IAuthEmailSender
{
    private readonly IEmailSender _emailSender;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthEmailSender(IEmailSender emailSender, IHttpContextAccessor httpContextAccessor)
    {
        _emailSender = emailSender;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task SendVerificationCodeAsync(string email, string code, CancellationToken ct)
    {
        var lang = ResolveLanguage();
        var subject = lang == "en"
            ? "DoorMarket - Verification code"
            : "DoorMarket - Code de verification";
        var body = lang == "en"
            ? $"""
Hello,

Your DoorMarket verification code is: {code}

This code expires in 15 minutes.
If you did not request this, please ignore this email.
"""
            : $"""
Bonjour,

Votre code de verification DoorMarket est : {code}

Ce code expire dans 15 minutes.
Si vous n'etes pas a l'origine de cette demande, ignorez ce message.
""";

        await _emailSender.SendAsync(email, subject, body, ct);
    }

    private string ResolveLanguage()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return "fr";
        }

        var appLang = context.Request.Headers["X-App-Lang"].ToString();
        if (!string.IsNullOrWhiteSpace(appLang))
        {
            return appLang.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "fr";
        }

        var acceptLang = context.Request.Headers["Accept-Language"].ToString();
        if (!string.IsNullOrWhiteSpace(acceptLang) &&
            acceptLang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return "en";
        }

        return "fr";
    }
}
