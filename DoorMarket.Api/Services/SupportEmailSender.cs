using System.Net;
using System.Net.Mail;

namespace DoorMarket.Api.Services;

public sealed class SupportEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SupportEmailSender> _logger;

    public SupportEmailSender(IConfiguration config, ILogger<SupportEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        var host = GetSupportValue("Host") ?? _config["Smtp:Host"];
        var from = GetSupportValue("FromEmail") ?? _config["Support:Email"] ?? _config["Smtp:FromEmail"];
        var user = GetSupportValue("Username") ?? _config["Smtp:Username"];
        var pass = GetSupportValue("Password") ?? _config["Smtp:Password"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            _logger.LogWarning("Support SMTP non configure (Host/FromEmail). Email non envoye vers {ToEmail}.", toEmail);
            return;
        }

        var port = host.Contains("hostinger", StringComparison.OrdinalIgnoreCase) ? 465 : 587;
        if (int.TryParse(GetSupportValue("Port") ?? _config["Smtp:Port"], out var parsedPort) && parsedPort > 0)
        {
            port = parsedPort;
        }

        var enableSsl = true;
        if (bool.TryParse(GetSupportValue("EnableSsl") ?? _config["Smtp:EnableSsl"], out var parsedSsl))
        {
            enableSsl = parsedSsl;
        }

        var timeoutSeconds = 12;
        if (int.TryParse(GetSupportValue("TimeoutSeconds") ?? _config["Smtp:TimeoutSeconds"], out var parsedTimeout) && parsedTimeout > 0)
        {
            timeoutSeconds = parsedTimeout;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(from, "DoorMarket"),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        async Task SendWithSettingsAsync(int targetPort, bool targetSsl, CancellationToken token)
        {
            using var client = new SmtpClient(host, targetPort)
            {
                EnableSsl = targetSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Timeout = Math.Max(5000, timeoutSeconds * 1000)
            };

            if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass))
            {
                client.Credentials = new NetworkCredential(user, pass);
            }

            using var reg = token.Register(client.SendAsyncCancel);
            await client.SendMailAsync(message).WaitAsync(token);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await SendWithSettingsAsync(port, enableSsl, timeoutCts.Token);
            _logger.LogInformation("Support email sent to {ToEmail} via {Host}:{Port}", toEmail, host, port);
        }
        catch (OperationCanceledException ex) when (host.Contains("hostinger", StringComparison.OrdinalIgnoreCase) && port == 465)
        {
            _logger.LogWarning(ex, "Support SMTP timeout on 465, retrying 587 (Hostinger).");
            using var retryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            retryCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            await SendWithSettingsAsync(587, true, retryCts.Token);
            _logger.LogInformation("Support email sent to {ToEmail} via {Host}:587", toEmail, host);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Support SMTP send failed to {ToEmail} via {Host}:{Port}", toEmail, host, port);
            var detail = ex.Message;
            if (ex is SmtpException smtpEx && smtpEx.InnerException is not null)
            {
                detail = $"{smtpEx.Message} ({smtpEx.InnerException.Message})";
            }

            throw new InvalidOperationException(
                $"Echec d'envoi email support via SMTP {host}:{port} (SSL={enableSsl}). Detail: {detail}");
        }
    }

    private string? GetSupportValue(string key)
        => _config[$"Support:Smtp:{key}"];
}
