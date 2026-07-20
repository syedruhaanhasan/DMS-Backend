using System.Text.Json;
using System.Text.Json.Serialization;

namespace WDAS.Application.Audit;

public sealed class AuditDetailsBuilder
{
    private readonly Dictionary<string, JsonElement?> _meta = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AuditChangeRecord> _changes = [];

    public static AuditDetailsBuilder Create() => new();

    public AuditDetailsBuilder Set(string key, object? value)
    {
        _meta[key] = ToJsonElement(value);
        return this;
    }

    public AuditDetailsBuilder Track(string field, object? from, object? to)
    {
        if (IsPasswordField(field))
        {
            if (!Equals(Normalize(from), Normalize(to)))
            {
                _changes.Add(new AuditChangeRecord(field, null, null, "Password updated"));
            }

            return this;
        }

        if (Equals(Normalize(from), Normalize(to)))
        {
            return this;
        }

        _changes.Add(new AuditChangeRecord(field, Format(from), Format(to), null));
        return this;
    }

    public AuditDetailsBuilder TrackCreated(string field, object? value)
    {
        if (IsPasswordField(field))
        {
            _changes.Add(new AuditChangeRecord(field, null, null, "Password set"));
            return this;
        }

        _changes.Add(new AuditChangeRecord(field, null, Format(value), null));
        return this;
    }

    public AuditDetailsBuilder TrackPasswordSet()
    {
        _changes.Add(new AuditChangeRecord("Password", null, null, "Password set"));
        return this;
    }

    public AuditDetailsBuilder TrackPasswordUpdated()
    {
        _changes.Add(new AuditChangeRecord("Password", null, null, "Password updated"));
        return this;
    }

    public string ToJson()
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in _meta)
        {
            payload[key] = value;
        }

        if (_changes.Count > 0)
        {
            payload["changes"] = _changes;
        }

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static bool IsPasswordField(string field) =>
        field.Contains("password", StringComparison.OrdinalIgnoreCase);

    private static string? Normalize(object? value) =>
        value switch
        {
            null => null,
            string s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(),
            bool b => b ? "true" : "false",
            _ => value.ToString()?.Trim()
        };

    private static string? Format(object? value) =>
        value switch
        {
            null => null,
            bool b => b ? "Yes" : "No",
            _ => value.ToString()?.Trim()
        };

    private static JsonElement? ToJsonElement(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return JsonSerializer.SerializeToElement(value);
    }

    private sealed record AuditChangeRecord(
        string Field,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? From,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? To,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message);
}
