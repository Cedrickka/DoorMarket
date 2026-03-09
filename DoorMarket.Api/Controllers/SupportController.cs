using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DoorMarket.Api.Services;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/support")]
public sealed class SupportController : ControllerBase
{
    private readonly SupportEmailSender _emailSender;
    private readonly IConfiguration _config;

    public SupportController(SupportEmailSender emailSender, IConfiguration config)
    {
        _emailSender = emailSender;
        _config = config;
    }

    [AllowAnonymous]
    [HttpPost("contact")]
    public async Task<IActionResult> Contact([FromBody] SupportContactRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Message))
        {
            return BadRequest("Email et message requis.");
        }

        var to = _config["Support:Email"]
                 ?? _config["Smtp:FromEmail"]
                 ?? "contact@door-market.com";

        var subject = string.IsNullOrWhiteSpace(req.Subject)
            ? "DoorMarket - Support"
            : $"DoorMarket - {req.Subject.Trim()}";

        var safeName = string.IsNullOrWhiteSpace(req.Name) ? "Client" : req.Name.Trim();
        var body = $"""
<p><b>Nom:</b> {safeName}</p>
<p><b>Email:</b> {req.Email.Trim()}</p>
<p><b>Message:</b></p>
<p>{System.Net.WebUtility.HtmlEncode(req.Message.Trim()).Replace("\n", "<br/>")}</p>
""";

        await _emailSender.SendAsync(to, subject, body, ct);
        return NoContent();
    }

    public sealed record SupportContactRequest(string Name, string Email, string Subject, string Message);
}
