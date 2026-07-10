using System.DirectoryServices.Protocols;
using LdapSearchRequest = System.DirectoryServices.Protocols.SearchRequest;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Application.Options;

namespace WDAS.Infrastructure.Identity;

public class LdapIdentityProvider : IIdentityProvider
{
    private readonly LdapOptions _options;
    private readonly ILogger<LdapIdentityProvider> _logger;

    public LdapIdentityProvider(IOptions<LdapOptions> options, ILogger<LdapIdentityProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("LDAP is not enabled. Set Ldap:Enabled=true in configuration.");
        }

        var entry = SearchUserEntry(username);
        if (entry is null)
        {
            return Task.FromResult<AuthenticatedUser?>(null);
        }

        var userDn = GetAttr(entry, "distinguishedName") ?? $"CN={username},{_options.UserSearchBase}";
        using var connection = CreateConnection();
        connection.Credential = new NetworkCredential(userDn, password);
        connection.AuthType = AuthType.Basic;
        connection.Bind();

        var adObjectId = GetObjectGuid(entry) ?? username;
        return Task.FromResult<AuthenticatedUser?>(new AuthenticatedUser(
            Guid.Empty,
            adObjectId,
            GetAttr(entry, "displayName") ?? username,
            GetAttr(entry, "mail") ?? $"{username}@local",
            Guid.Empty,
            []));
    }

    public Task<IReadOnlyList<DirectoryUserSnapshot>> GetDirectoryUsersAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("LDAP is not enabled.");
        }

        using var connection = CreateConnection();
        BindServiceAccount(connection);

        var searchBase = string.IsNullOrWhiteSpace(_options.UserSearchBase) ? _options.BaseDn : _options.UserSearchBase;
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

        return Task.FromResult<IReadOnlyList<DirectoryUserSnapshot>>(users);
    }

    public Task<IReadOnlyList<DirectoryDepartmentSnapshot>> GetDirectoryDepartmentsAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("LDAP is not enabled.");
        }

        using var connection = CreateConnection();
        BindServiceAccount(connection);

        var request = new LdapSearchRequest(_options.BaseDn, "(objectClass=organizationalUnit)", SearchScope.Subtree, "ou", "description");
        var response = (SearchResponse)connection.SendRequest(request);
        var departments = new List<DirectoryDepartmentSnapshot>();

        foreach (SearchResultEntry entry in response.Entries)
        {
            var name = GetAttr(entry, "ou");
            if (string.IsNullOrWhiteSpace(name)) continue;
            var code = name.Length >= 3 ? name[..3].ToUpperInvariant() : name.ToUpperInvariant();
            departments.Add(new DirectoryDepartmentSnapshot(name, name, code, null));
        }

        return Task.FromResult<IReadOnlyList<DirectoryDepartmentSnapshot>>(departments);
    }

    private LdapConnection CreateConnection()
    {
        var identifier = new LdapDirectoryIdentifier(_options.Host, _options.Port, _options.UseSsl, false);
        return new LdapConnection(identifier) { Timeout = TimeSpan.FromSeconds(15) };
    }

    private void BindServiceAccount(LdapConnection connection)
    {
        connection.Credential = new NetworkCredential(_options.BindDn, _options.BindPassword);
        connection.AuthType = AuthType.Basic;
        connection.Bind();
    }

    private SearchResultEntry? SearchUserEntry(string username)
    {
        using var connection = CreateConnection();
        BindServiceAccount(connection);

        var searchBase = string.IsNullOrWhiteSpace(_options.UserSearchBase) ? _options.BaseDn : _options.UserSearchBase;
        var filter = string.Format(_options.UserFilter, username);
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
