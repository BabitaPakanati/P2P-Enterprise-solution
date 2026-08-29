namespace P2P.Application.Auth;

/// <summary>The claims a signed token carries - one organisation, one user, resolved once at login and never re-derived per-request.</summary>
public sealed record TokenClaims(Guid UserId, string Email, string DisplayName, Guid OrganisationId, string OrgCode, string SchemaName);

public interface IJwtTokenService
{
    string CreateToken(TokenClaims claims);
}

public sealed record LoginResult(string Token, Guid UserId, string DisplayName, string Email, Guid OrganisationId, string OrgCode, string OrgDisplayName);

/// <summary>
/// Replaces the X-Org-Code/X-User-Id header stand-ins for every endpoint except the
/// handful of dev-only bootstrap diagnostics - see IdentityResolutionMiddleware.
/// </summary>
public interface IAuthService
{
    Task<LoginResult> LoginAsync(string orgCode, string email, string password, CancellationToken ct = default);
}
