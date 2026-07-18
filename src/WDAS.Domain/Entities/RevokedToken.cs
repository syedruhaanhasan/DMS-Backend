using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

/// <summary>
/// A JWT that has been explicitly invalidated (e.g. on sign-out) before its natural
/// expiry. Access tokens are stateless, so revoked ids are checked on every request
/// until <see cref="ExpiresAtUtc"/> passes, after which the row can be purged.
/// </summary>
public class RevokedToken : Entity
{
    /// <summary>The JWT id ("jti") claim of the revoked token.</summary>
    public string Jti { get; set; } = string.Empty;

    /// <summary>When the underlying token expires; the row is safe to drop afterwards.</summary>
    public DateTime ExpiresAtUtc { get; set; }
}
