using Microsoft.Extensions.Logging;
using WDAS.Application.Abstractions;

namespace WDAS.Infrastructure.Notifications;

public class DevPushNotificationSender : IPushNotificationSender
{
    private readonly ILogger<DevPushNotificationSender> _logger;

    public DevPushNotificationSender(ILogger<DevPushNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(int userId, string title, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Push notification to {UserId}: {Title} - {Body}", userId, title, body);
        return Task.CompletedTask;
    }
}
