using System.Text.Json;
using WDAS.Domain.Enums;

namespace WDAS.Application.Notifications;

public static class NotificationChannelHelper
{
    public static IReadOnlyList<NotificationChannel> FilterByJsonSettings(
        IReadOnlyCollection<NotificationChannel> channels,
        string settingsJson,
        NotificationEventType eventType)
    {
        var eventKey = MapEventToSettingsKey(eventType);
        if (eventKey is null)
        {
            return channels.ToList();
        }

        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            if (!doc.RootElement.TryGetProperty(eventKey, out var eventPrefs))
            {
                return channels.ToList();
            }

            var filtered = new List<NotificationChannel>();
            foreach (var channel in channels)
            {
                var prefKey = MapChannelToPreferenceKey(channel);
                if (prefKey is null)
                {
                    filtered.Add(channel);
                    continue;
                }

                if (!eventPrefs.TryGetProperty(prefKey, out var enabled) || enabled.GetBoolean())
                {
                    filtered.Add(channel);
                }
            }

            return filtered;
        }
        catch (JsonException)
        {
            return channels.ToList();
        }
    }

    public static string? MapEventToSettingsKey(NotificationEventType eventType) =>
        eventType switch
        {
            NotificationEventType.SubmittedForApproval => "submit",
            NotificationEventType.ApprovalRecorded => "approve",
            NotificationEventType.Rejected => "reject",
            NotificationEventType.ReturnedForCorrection => "return",
            NotificationEventType.SlaBreach => "reminder",
            NotificationEventType.Finalized => "finalize",
            NotificationEventType.Cancelled => "reject",
            _ => null
        };

    private static string? MapChannelToPreferenceKey(NotificationChannel channel) =>
        channel switch
        {
            NotificationChannel.Email => "email",
            NotificationChannel.InApp => "inApp",
            NotificationChannel.SmsWhatsApp => "sms",
            _ => null
        };
}
