using System.DirectoryServices.Protocols;
using LdapSearchRequest = System.DirectoryServices.Protocols.SearchRequest;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Application.Options;

namespace WDAS.Infrastructure.Identity;

public class LdapIdentityProvider : IIdentityProvider
{
    private readonly LdapOptions _options;
    private readonly IApplicationDbContext _db;
    private readonly ILogger<LdapIdentityProvider> _logger;

    public LdapIdentityProvider(
        IOptions<LdapOptions> options,
        IApplicationDbContext db,
        ILogger<LdapIdentityProvider> logger)
    {
        _options = options.Value;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Effective connection settings: the database row (managed from the UI) takes
    /// precedence for enabled/host/port/ssl; advanced fields come from appsettings.
    /// </summary>
    private sealed record EffectiveLdapSettings(
        bool Enabled,
        string Host,
        int Port,
        bool UseSsl,
        string BaseDn,
        string BindDn,
        string BindPassword,
        string UserFilter,
        string UserSearchBase);

    private async Task<EffectiveLdapSettings> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        var row = await _db.ActiveDirectorySettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return new EffectiveLdapSettings(
            Enabled: row?.Enabled ?? _options.Enabled,
            Host: !string.IsNullOrWhiteSpace(row?.DomainName) ? row!.DomainName : _options.Host,
            Port: row is { Port: > 0 } ? row.Port : _options.Port,
            UseSsl: row?.UseSsl ?? _options.UseSsl,
            BaseDn: _options.BaseDn,
            BindDn: _options.BindDn,
            BindPassword: _options.BindPassword,
            UserFilter: string.IsNullOrWhiteSpace(_options.UserFilter) ? "(&(objectClass=user)(sAMAccountName={0}))" : _options.UserFilter,
            UserSearchBase: _options.UserSearchBase);
    }

    public async Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("Active Directory is not enabled. Enable it under Configuration → Active Directory.");
        }

        var entry = SearchUserEntry(settings, username);
        if (entry is null)
        {
            return null;
        }

        var userDn = GetAttr(entry, "distinguishedName") ?? $"CN={username},{settings.UserSearchBase}";
        using var connection = CreateConnection(settings);
        connection.Credential = new NetworkCredential(userDn, password);
        connection.AuthType = AuthType.Basic;
        connection.Bind();

        var adObjectId = GetObjectGuid(entry) ?? username;
        return new AuthenticatedUser(
            0,
            adObjectId,
            GetAttr(entry, "displayName") ?? username,
            GetAttr(entry, "mail") ?? $"{username}@local",
            0,
            []);
    }

    public async Task<IReadOnlyList<DirectoryUserSnapshot>> GetDirectoryUsersAsync(CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("Active Directory is not enabled.");
        }

        using var connection = CreateConnection(settings);
        BindServiceAccount(connection, settings);

        var searchBase = string.IsNullOrWhiteSpace(settings.UserSearchBase) ? settings.BaseDn : settings.UserSearchBase;
        var request = new LdapSearchRequest(searchBase, "(&(objectClass=user)(objectCategory=person))", SearchScope.Subtree,
            "objectGUID", "sAMAccountName", "displayName", "mail", "title", "department", "manager", "userAccountControl");
        var response = (SearchResponse)connection.SendRequest(request);

        var users = new List<DirectoryUserSnapshot>();
        foreach (SearchResultEntry entry in response.Entries)
        {
            var sam = GetAttr(entry, "sAMAccountName");
            if (string.IsNullOrWhiteSpace(sam)) continue;

            var uac = int.TryParse(GetAttr(entry, "userAccountControl"), out var flags) ? flags : 512;
            var enabled = (uac & 2) == 0;

            users.Add(new DirectoryUserSnapshot(
                GetObjectGuid(entry) ?? sam,
                sam,
                GetAttr(entry, "displayName") ?? sam,
                GetAttr(entry, "mail") ?? $"{sam}@local",
                GetAttr(entry, "title") ?? "Staff",
                GetAttr(entry, "department") ?? "General",
                GetAttr(entry, "manager"),
                enabled));
        }

        return users;
    }

    public async Task<IReadOnlyList<DirectoryDepartmentSnapshot>> GetDirectoryDepartmentsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("Active Directory is not enabled.");
        }

        using var connection = CreateConnection(settings);
        BindServiceAccount(connection, settings);

        var request = new LdapSearchRequest(settings.BaseDn, "(objectClass=organizationalUnit)", SearchScope.Subtree, "ou", "description");
        var response = (SearchResponse)connection.SendRequest(request);
        var departments = new List<DirectoryDepartmentSnapshot>();

        foreach (SearchResultEntry entry in response.Entries)
        {
            var name = GetAttr(entry, "ou");
            if (string.IsNullOrWhiteSpace(name)) continue;
            var code = name.Length >= 3 ? name[..3].ToUpperInvariant() : name.ToUpperInvariant();
            departments.Add(new DirectoryDepartmentSnapshot(name, name, code, null));
        }

        return departments;
    }

    private static LdapConnection CreateConnection(EffectiveLdapSettings settings)
    {
        var identifier = new LdapDirectoryIdentifier(settings.Host, settings.Port, settings.UseSsl, false);
        return new LdapConnection(identifier) { Timeout = TimeSpan.FromSeconds(15) };
    }

    private static void BindServiceAccount(LdapConnection connection, EffectiveLdapSettings settings)
    {
        connection.Credential = new NetworkCredential(settings.BindDn, settings.BindPassword);
        connection.AuthType = AuthType.Basic;
        connection.Bind();
    }

    private static SearchResultEntry? SearchUserEntry(EffectiveLdapSettings settings, string username)
    {
        using var connection = CreateConnection(settings);
        BindServiceAccount(connection, settings);

        var searchBase = string.IsNullOrWhiteSpace(settings.UserSearchBase) ? settings.BaseDn : settings.UserSearchBase;
        var filter = string.Format(settings.UserFilter, username);
        var request = new LdapSearchRequest(searchBase, filter, SearchScope.Subtree,
            "distinguishedName", "objectGUID", "sAMAccountName", "displayName", "mail", "title", "department", "manager");
        var response = (SearchResponse)connection.SendRequest(request);
        return response.Entries.Count > 0 ? response.Entries[0] : null;
    }

    private static string? GetAttr(SearchResultEntry entry, string name)
    {
        if (!entry.Attributes.Contains(name)) return null;
        var values = entry.Attributes[name];
        return values?.Count > 0 ? values[0]?.ToString() : null;
    }

    private static string? GetObjectGuid(SearchResultEntry entry)
    {
        if (!entry.Attributes.Contains("objectGUID")) return null;
        var bytes = (byte[])entry.Attributes["objectGUID"][0]!;
        return new Guid(bytes).ToString();
    }
}
