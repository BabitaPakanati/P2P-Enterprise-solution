using P2P.Domain.Common;

namespace P2P.Domain.Platform;

/// <summary>
/// A user who operates across organisations rather than inside one - creating new
/// orgs, and (later) organisation-level field/workflow configuration. Deliberately
/// separate from P2P.Domain.Identity.User: an org's own users live in that org's
/// schema and a token for one always carries org_id/schema claims; a platform admin
/// lives in the `platform` schema and their token carries neither - see
/// IdentityResolutionMiddleware and the "PlatformAdmin" authorization policy.
/// </summary>
public sealed class PlatformAdminUser : Entity
{
    public string Email { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
