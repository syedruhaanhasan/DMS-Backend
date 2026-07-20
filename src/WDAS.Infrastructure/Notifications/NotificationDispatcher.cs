using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WDAS.Application.Abstractions;
using WDAS.Application.Notifications;
using WDAS.Application.Options;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;

namespace WDAS.Infrastructure.Notifications;

public class NotificationDispatcher : INotificationDispatcher
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<NotificationDispatcher> _logger;
    private readonly IPushNotificationSender _pushSender;
    private readonly IEmailSender _emailSender;
    private readonly ISmsSender _smsSender;
    private readonly ExternalApproverOptions _externalOptions;

    public NotificationDispatcher(
        IApplicationDbContext db,
        IClock clock,
        ILogger<NotificationDispatcher> logger,
        IPushNotificationSender pushSender,
        IEmailSender emailSender,
        ISmsSender smsSender,
        IOptions<ExternalApproverOptions> externalOptions)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
        _pushSender = pushSender;
        _emailSender = emailSender;
        _smsSender = smsSender;
        _externalOptions = externalOptions.Value;
    }

    public async Task DispatchAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var (recipientEmail, recipientPhone) = await ResolveRecipientContactAsync(request, cancellationToken);
        var channels = await ResolveChannelsAsync(request, cancellationToken);

        if (channels.Count == 0)
        {
            _logger.LogDebug("Notification suppressed — no channels: {Event}", request.EventType);
            return;
        }

        var now = _clock.UtcNow;
        var shouldPush = request.RecipientUserId.HasValue && ShouldSendPush(request.EventType);

        // Prefer in-app persistence first so submit UX is not gated on SMTP/SMS.
        var orderedChannels = channels
            .OrderBy(c => c == NotificationChannel.InApp ? 0 : c == NotificationChannel.Email ? 1 : 2)
            .ToList();

        foreach (var channel in orderedChannels)
        {
            try
            {
                if (channel == NotificationChannel.Email)
                {
                    await SendEmailAsync(request, recipientEmail, now, cancellationToken);
                    continue;
                }

                if (channel == NotificationChannel.SmsWhatsApp)
                {
                    await SendSmsAsync(request, recipientPhone, now, cancellationToken);
                    continue;
                }

                if (channel == NotificationChannel.InApp)
                {
                    await PersistInAppAsync(request, recipientEmail, now, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification dispatch failed for {Event} via {Channel}", request.EventType, channel);
                _db.Add(new Notification
                {
                    RecipientUserId = request.RecipientUserId,
                    RecipientEmail = recipientEmail,
                    EventType = request.EventType,
                    Channel = channel,
                    Status = NotificationDeliveryStatus.Failed,
                    DocumentId = request.DocumentId,
                    WorkflowStepId = request.WorkflowStepId,
                    Subject = request.Subject,
                    Body = request.Body,
                    LastError = ex.Message,
                    RetryCount = 1,
                    CreatedAtUtc = now
                });
            }
        }

        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (shouldPush)
        {
            try
            {
                await _pushSender.SendAsync(request.RecipientUserId!.Value, request.Subject, request.Body, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push notification failed for user {UserId}", request.RecipientUserId);
            }
        }
    }

    private async Task<(string? Email, string? Phone)> ResolveRecipientContactAsync(
        NotificationRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.RecipientEmail;
        var phone = request.RecipientPhone;

        if (!request.RecipientUserId.HasValue)
        {
            return (email, phone);
        }

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == request.RecipientUserId.Value)
            .Select(u => new { u.Email, u.PhoneNumber })
            .FirstOrDefaultAsync(cancellationToken);

        email ??= user?.Email;
        phone ??= user?.PhoneNumber;
        return (email, phone);
    }

    private async Task<IReadOnlyList<NotificationChannel>> ResolveChannelsAsync(
        NotificationRequest request,
        CancellationToken cancellationToken)
    {
        var channels = request.Channels?.ToList()
            ?? [NotificationChannel.InApp, NotificationChannel.Email];

        if (request.EventType == NotificationEventType.ExternalOtp)
        {
            return [NotificationChannel.Email];
        }

        if (request.DocumentId.HasValue)
        {
            var workflowSettings = await _db.Documents.AsNoTracking()
                .Where(d => d.Id == request.DocumentId.Value)
                .Select(d => d.WorkflowVersion!.NotificationSettingsJson)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(workflowSettings))
            {
                channels = NotificationChannelHelper
                    .FilterByJsonSettings(channels, workflowSettings, request.EventType)
                    .ToList();
            }
        }

        channels = (await FilterChannelsByUserPreferencesAsync(channels, request, cancellationToken)).ToList();

        return channels;
    }

    private async Task SendEmailAsync(
        NotificationRequest request,
        string? recipientEmail,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var notification = CreateNotification(request, recipientEmail, NotificationChannel.Email, now);

        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            notification.Status = NotificationDeliveryStatus.Failed;
            notification.LastError = "Recipient email is not available.";
            _db.Add(notification);
            _logger.LogWarning("Email skipped for {Event} — no recipient email", request.EventType);
            return;
        }

        _db.Add(notification);
        try
        {
            string body;
            string? htmlBody = request.HtmlBody;
            var isHtml = !string.IsNullOrWhiteSpace(htmlBody);
            if (isHtml)
            {
                body = htmlBody!;
            }
            else
            {
                var built = WorkflowNotificationEmailBuilder.Build(
                    _externalOptions.AppBaseUrl,
                    request.Subject,
                    request.Body,
                    request.DocumentId);
                body = built.HtmlBody;
                isHtml = true;
            }

            await _emailSender.SendAsync(recipientEmail, request.Subject, body, isHtml, cancellationToken);
            _logger.LogInformation("Email dispatched: {Event} to {Recipient}", request.EventType, recipientEmail);
        }
        catch (Exception emailEx)
        {
            _logger.LogWarning(emailEx, "Email send failed for {Event}", request.EventType);
            notification.Status = NotificationDeliveryStatus.Failed;
            notification.LastError = emailEx.Message;
        }
    }

    private async Task SendSmsAsync(
        NotificationRequest request,
        string? recipientPhone,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var notification = CreateNotification(request, null, NotificationChannel.SmsWhatsApp, now);

        if (string.IsNullOrWhiteSpace(recipientPhone))
        {
            notification.Status = NotificationDeliveryStatus.Failed;
            notification.LastError = "Recipient phone number is not available.";
            _db.Add(notification);
            _logger.LogWarning("SMS skipped for {Event} — no recipient phone", request.EventType);
            return;
        }

        _db.Add(notification);
        try
        {
            await _smsSender.SendAsync(recipientPhone, $"{request.Subject}: {request.Body}", cancellationToken);
            _logger.LogInformation("SMS dispatched: {Event} to {Recipient}", request.EventType, recipientPhone);
        }
        catch (Exception smsEx)
        {
            _logger.LogWarning(smsEx, "SMS send failed for {Event}", request.EventType);
            notification.Status = NotificationDeliveryStatus.Failed;
            notification.LastError = smsEx.Message;
        }
    }

    private Task PersistInAppAsync(
        NotificationRequest request,
        string? recipientEmail,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!request.RecipientUserId.HasValue)
        {
            _logger.LogDebug("In-app notification skipped — no recipient user id for {Event}", request.EventType);
            return Task.CompletedTask;
        }

        _db.Add(CreateNotification(request, recipientEmail, NotificationChannel.InApp, now));
        _logger.LogInformation(
            "In-app notification: {Event} to user {Recipient}",
            request.EventType,
            request.RecipientUserId);
        return Task.CompletedTask;
    }

    private static Notification CreateNotification(
        NotificationRequest request,
        string? recipientEmail,
        NotificationChannel channel,
        DateTime now) =>
        new()
        {
            RecipientUserId = request.RecipientUserId,
            RecipientEmail = recipientEmail,
            EventType = request.EventType,
            Channel = channel,
            Status = NotificationDeliveryStatus.Sent,
            DocumentId = request.DocumentId,
            WorkflowStepId = request.WorkflowStepId,
            Subject = request.Subject,
            Body = request.Body,
            SentAtUtc = now,
            DeliveredAtUtc = now,
            CreatedAtUtc = now
        };

    private async Task<IReadOnlyList<NotificationChannel>> FilterChannelsByUserPreferencesAsync(
        IReadOnlyList<NotificationChannel> channels,
        NotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.RecipientUserId.HasValue ||
            request.EventType is NotificationEventType.ExternalOtp or NotificationEventType.DelegationNotice)
        {
            return channels;
        }

        var prefsJson = await _db.Users.AsNoTracking()
            .Where(u => u.Id == request.RecipientUserId.Value)
            .Select(u => u.NotificationPreferencesJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(prefsJson))
        {
            return channels;
        }

        return NotificationChannelHelper.FilterByJsonSettings(channels, prefsJson, request.EventType).ToList();
    }

    private static bool ShouldSendPush(NotificationEventType eventType) =>
        eventType is NotificationEventType.SubmittedForApproval
            or NotificationEventType.ApprovalRecorded
            or NotificationEventType.AddedAsReviewer
            or NotificationEventType.ReviewCompleted
            or NotificationEventType.Rejected
            or NotificationEventType.ReturnedForCorrection
            or NotificationEventType.SlaBreach
            or NotificationEventType.Cancelled
            or NotificationEventType.Finalized
            or NotificationEventType.DelegationNotice;
}
