namespace P2P.Application.Auth;

/// <summary>The claims a signed token carries - one organisation, one user, resolved once at login and never re-derived per-request.</summary>
public sealed record TokenClaims(Guid UserId, string Email, string DisplayName, Guid OrganisationId, string OrgCode, string SchemaName);

/// <summary>No organisation at all - a platform admin's token is deliberately shaped differently, see IdentityResolutionMiddleware.</summary>
public sealed record PlatformAdminTokenClaims(Guid AdminId, string Email, string DisplayName);

public interface IJwtTokenService
{
    string CreateToken(TokenClaims claims);
    string CreatePlatformAdminToken(PlatformAdminTokenClaims claims);
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

public sealed record PlatformLoginResult(string Token, Guid AdminId, string DisplayName, string Email);

/// <summary>Authenticates a platform admin - operates against PlatformDbContext only, never a tenant schema.</summary>
public interface IPlatformAuthService
{
    Task<PlatformLoginResult> LoginAsync(string email, string password, CancellationToken ct = default);
}
