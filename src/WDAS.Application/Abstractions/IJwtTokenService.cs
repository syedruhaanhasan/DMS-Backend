using WDAS.Application.Models;

namespace WDAS.Application.Abstractions;

public interface IJwtTokenService
{
    string CreateToken(AuthenticatedUser user);
    string CreateExternalSessionToken(Guid sessionId, Guid workflowStepId, Guid documentId, DateTime expiresAtUtc);
}
