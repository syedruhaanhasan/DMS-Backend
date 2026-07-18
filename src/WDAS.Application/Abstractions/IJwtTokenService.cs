using WDAS.Application.Models;

namespace WDAS.Application.Abstractions;

public interface IJwtTokenService
{
    AccessTokenResult CreateToken(AuthenticatedUser user);
    string CreateExternalSessionToken(int sessionId, int workflowStepId, int documentId, DateTime expiresAtUtc);
}
