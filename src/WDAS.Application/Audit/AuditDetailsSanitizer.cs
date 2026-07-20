using System.Text.Json;

namespace WDAS.Application.Audit;

public static class AuditDetailsSanitizer
{
    private static readonly string[] SensitiveKeys =
    [
        "password",
        "passwordhash",
        "bindpassword",
        "secret",
        "token"
    ];

    public static string? Sanitize(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
        {
            return detailsJson;
        }

        try
        {
            using var doc = JsonDocument.Parse(detailsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return detailsJson;
            }

            var sanitized = SanitizeObject(doc.RootElement);
            return JsonSerializer.Serialize(sanitized);
        }
        catch (JsonException)
        {
            return detailsJson;
        }
    }

    private static Dictionary<string, object?> SanitizeObject(JsonElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            if (IsSensitiveKey(property.Name))
            {
                result[property.Name] = "Password updated";
                continue;
            }

            result[property.Name] = SanitizeValue(property.Value, property.Name);
        }

        return result;
    }

    private static object? SanitizeValue(JsonElement value, string? propertyName)
    {
        if (propertyName is not null && IsSensitiveKey(propertyName))
        {
            return "Password updated";
        }

        return value.ValueKind switch
        {
            JsonValueKind.Object => SanitizeObject(value),
            JsonValueKind.Array => value.EnumerateArray()
                .Select(item => SanitizeValue(item, null))
                .ToList(),
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.TryGetInt64(out var i) ? i : value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => value.GetRawText()
        };
    }

    private static bool IsSensitiveKey(string key)
    {
        foreach (var sensitive in SensitiveKeys)
        {
            if (key.Contains(sensitive, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
