namespace WDAS.Application.Models;

public record ActiveDirectorySettingsDto(
    bool Enabled,
    string DomainName,
    int Port,
    bool UseSsl,
    DateTime? UpdatedAtUtc);

/// <summary>Lightweight status returned to any authenticated caller (no connection details).</summary>
public record ActiveDirectoryStatusDto(bool Enabled);

public record UpdateActiveDirectorySettingsRequest(
    bool Enabled,
    string? DomainName,
    int? Port,
    bool? UseSsl);
