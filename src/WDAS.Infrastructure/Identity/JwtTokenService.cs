using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;

namespace WDAS.Infrastructure.Identity;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "WDAS";
    public string Audience { get; set; } = "WDAS.Clients";
    public string SigningKey { get; set; } = "WDAS-Phase1-Dev-Signing-Key-Change-In-Production-123456";
    public int ExpiryHours { get; set; } = 8;
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public AccessTokenResult CreateToken(AuthenticatedUser user)
    {
        var jti = Guid.NewGuid().ToString("N");
        var expiresAtUtc = DateTime.UtcNow.AddHours(_options.ExpiryHours);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, jti),
            new("ad_oid", user.AdObjectId),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("department_id", user.DepartmentId.ToString())
        };

        foreach (var roleCode in user.RoleCodes.Distinct())
        {
            claims.Add(new Claim(ClaimTypes.Role, roleCode));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc, jti);
    }

    public string CreateExternalSessionToken(int sessionId, int workflowStepId, int documentId, DateTime expiresAtUtc)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sessionId.ToString()),
            new("external_session", "true"),
            new("workflow_step_id", workflowStepId.ToString()),
            new("document_id", documentId.ToString()),
            new(ClaimTypes.Role, "ExternalApprover")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
