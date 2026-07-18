namespace WDAS.Application.Abstractions;

/// <summary>
/// Tracks access tokens that have been invalidated before their natural expiry
/// (e.g. on sign-out). Because JWTs are stateless, every authenticated request
/// checks the revocation list so a signed-out token stops working immediately.
/// </summary>
public interface ITokenRevocationService
{
    /// <summary>Marks a token id ("jti") as revoked until it would have expired.</summary>
    Task RevokeAsync(string jti, DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    /// <summary>True when the given token id has been revoked and is not yet expired.</summary>
    Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default);
}
