using System.Net;
using System.Net.Mail;

namespace DoorMarket.Api.Services;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        var host = _config["Smtp:Host"];
        var from = _config["Smtp:FromEmail"];
        var user = _config["Smtp:Username"];
        var pass = _config["Smtp:Password"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            _logger.LogWarning("SMTP non configure (Smtp:Host / Smtp:FromEmail). Email non envoye vers {ToEmail}.", toEmail);
            return;
        }

        var port = host.Contains("hostinger", StringComparison.OrdinalIgnoreCase) ? 465 : 587;
        if (int.TryParse(_config["Smtp:Port"], out var parsedPort) && parsedPort > 0)
        {
            port = parsedPort;
        }

        var enableSsl = true;
        if (bool.TryParse(_config["Smtp:EnableSsl"], out var parsedSsl))
        {
            enableSsl = parsedSsl;
        }

        var timeoutSeconds = 12;
        if (int.TryParse(_config["Smtp:TimeoutSeconds"], out var parsedTimeout) && parsedTimeout > 0)
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
            _logger.LogInformation("SMTP email sent to {ToEmail} via {Host}:{Port}", toEmail, host, port);
        }
        catch (OperationCanceledException ex) when (host.Contains("hostinger", StringComparison.OrdinalIgnoreCase) && port == 465)
        {
            _logger.LogWarning(ex, "SMTP send timeout on 465, retrying 587 (Hostinger).");
            try
            {
                using var retryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                retryCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                await SendWithSettingsAsync(587, true, retryCts.Token);
                _logger.LogInformation("SMTP email sent to {ToEmail} via {Host}:587", toEmail, host);
                return;
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "SMTP retry failed to {ToEmail} via {Host}:587", toEmail, host);
                var detail = retryEx.Message;
                if (retryEx is SmtpException smtpEx && smtpEx.InnerException is not null)
                {
                    detail = $"{smtpEx.Message} ({smtpEx.InnerException.Message})";
                }

                throw new InvalidOperationException(
                    $"Echec d'envoi email via SMTP {host}:587 (SSL=true). Detail: {detail}");
            }
        }
        catch (OperationCanceledException ex)
        {
            var detail = ex.Message;
            _logger.LogError(ex, "SMTP send timeout to {ToEmail} via {Host}:{Port}", toEmail, host, port);
            throw new InvalidOperationException(
                $"Echec d'envoi email via SMTP {host}:{port} (SSL={enableSsl}). Timeout apres {timeoutSeconds}s. Detail: {detail}");
        }
        catch (SmtpException ex) when (host.Contains("hostinger", StringComparison.OrdinalIgnoreCase) && port == 465)
        {
            _logger.LogWarning(ex, "SMTP send failed on 465, retrying 587 (Hostinger).");
            try
            {
                using var retryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                retryCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                await SendWithSettingsAsync(587, true, retryCts.Token);
                _logger.LogInformation("SMTP email sent to {ToEmail} via {Host}:587", toEmail, host);
                return;
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "SMTP retry failed to {ToEmail} via {Host}:587", toEmail, host);
                var detail = retryEx.Message;
                if (retryEx is SmtpException smtpEx && smtpEx.InnerException is not null)
                {
                    detail = $"{smtpEx.Message} ({smtpEx.InnerException.Message})";
                }

                throw new InvalidOperationException(
                    $"Echec d'envoi email via SMTP {host}:587 (SSL=true). Detail: {detail}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed to {ToEmail} via {Host}:{Port}", toEmail, host, port);
            var detail = ex.Message;
            if (ex is SmtpException smtpEx && smtpEx.InnerException is not null)
            {
                detail = $"{smtpEx.Message} ({smtpEx.InnerException.Message})";
            }

            throw new InvalidOperationException(
                $"Echec d'envoi email via SMTP {host}:{port} (SSL={enableSsl}). Detail: {detail}");
        }
    }
}
