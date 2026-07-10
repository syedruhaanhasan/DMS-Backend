using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WDAS.Application.Abstractions;
using WDAS.Application.Options;

namespace WDAS.Infrastructure.Notifications;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body, bool isHtml = false, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Email disabled — would send to {To}: {Subject}\n{Body}", toEmail, subject, body);
            return;
        }

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromDisplayName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml,
        };
        message.To.Add(toEmail);

        await client.SendMailAsync(message, cancellationToken);
        _logger.LogInformation("Email sent to {To}: {Subject}", toEmail, subject);
    }
}
