using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using P2P.Application.Auth;

namespace P2P.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string SigningKey { get; set; } = default!;
    public string Issuer { get; set; } = "p2p-platform";
    public string Audience { get; set; } = "p2p-platform";
    public int ExpiryMinutes { get; set; } = 480;
}

/// <summary>
/// Self-hosted JWT issuance - no external identity provider yet. This is the honest
/// middle ground for where the project is: real signed tokens instead of trust-me
/// headers, but still short of Cognito/Auth0/SSO (see docs/ARCHITECTURE.md's
/// decision log - that's flagged as revisit-when-a-customer-needs-SSO, not done now).
/// Swapping this out later means changing what validates the token, not how every
/// endpoint reads identity from it - they only ever see claims.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public string CreateToken(TokenClaims claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwtClaims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, claims.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, claims.Email),
            new Claim("name", claims.DisplayName),
            new Claim("org_id", claims.OrganisationId.ToString()),
            new Claim("org_code", claims.OrgCode),
            new Claim("schema", claims.SchemaName),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: jwtClaims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreatePlatformAdminToken(PlatformAdminTokenClaims claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Deliberately no org_id/org_code/schema claims - a platform admin isn't
        // scoped to a tenant. "platform_admin" is what the PlatformAdmin
        // authorization policy checks, and what IdentityResolutionMiddleware reads
        // to skip tenant/user resolution entirely for this token type.
        var jwtClaims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, claims.AdminId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, claims.Email),
            new Claim("name", claims.DisplayName),
            new Claim("platform_admin", "true"),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: jwtClaims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
