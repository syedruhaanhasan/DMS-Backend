using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WDAS.Application.Abstractions;
using WDAS.Application.Options;

namespace WDAS.Infrastructure.Notifications;

public class TwilioSmsSender : ISmsSender
{
    private readonly SmsOptions _options;
    private readonly ILogger<TwilioSmsSender> _logger;
    private readonly HttpClient _http;

    public TwilioSmsSender(IOptions<SmsOptions> options, ILogger<TwilioSmsSender> logger, HttpClient http)
    {
        _options = options.Value;
        _logger = logger;
        _http = http;
    }

    public async Task SendAsync(string toPhoneOrEmail, string message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("SMS disabled — would send to {To}: {Message}", toPhoneOrEmail, message);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.AccountSid) || string.IsNullOrWhiteSpace(_options.AuthToken))
        {
            throw new InvalidOperationException("SMS is enabled but Twilio credentials are not configured.");
        }

        var url = $"https://api.twilio.com/2010-04-01/Accounts/{_options.AccountSid}/Messages.json";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.AccountSid}:{_options.AuthToken}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = toPhoneOrEmail,
            ["From"] = _options.FromNumber,
            ["Body"] = message,
        });

        var res = await _http.SendAsync(req, cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Twilio SMS failed: {res.StatusCode} {body}");
        }

        _logger.LogInformation("SMS sent to {To}", toPhoneOrEmail);
    }
}
