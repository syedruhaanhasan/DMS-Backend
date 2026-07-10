using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WDAS.Application.Abstractions;
using WDAS.Application.Options;

namespace WDAS.Infrastructure.Notifications;

public class ExpoPushNotificationSender : IPushNotificationSender
{
    private readonly IApplicationDbContext _db;
    private readonly PushOptions _options;
    private readonly ILogger<ExpoPushNotificationSender> _logger;
    private readonly HttpClient _http;

    public ExpoPushNotificationSender(
        IApplicationDbContext db,
        IOptions<PushOptions> options,
        ILogger<ExpoPushNotificationSender> logger,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
        _http = httpClientFactory.CreateClient("ExpoPush");
    }

    public async Task SendAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Push disabled — would send to {UserId}: {Title} - {Body}", userId, title, body);
            return;
        }

        var tokens = await _db.PushDeviceRegistrations
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(d => d.DeviceToken)
            .ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            _logger.LogDebug("No push devices registered for user {UserId}", userId);
            return;
        }

        var payload = tokens.Select(token => new ExpoPushMessage(token, title, body)).ToList();
        using var response = await _http.PostAsJsonAsync(_options.ExpoApiUrl, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Expo push failed: {response.StatusCode} {error}");
        }

        _logger.LogInformation("Push sent to {Count} device(s) for user {UserId}: {Title}", tokens.Count, userId, title);
    }

    private sealed record ExpoPushMessage(
        [property: JsonPropertyName("to")] string To,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("sound")] string Sound = "default");
}
