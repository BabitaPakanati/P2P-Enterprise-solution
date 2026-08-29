using P2P.Application.Abstractions;

namespace P2P.Infrastructure.MultiTenancy;

/// <summary>
/// Scoped (one instance per request/job). Set once, early, by
/// IdentityResolutionMiddleware - everything downstream (AppDbContext, handlers,
/// audit writer) reads it, nothing downstream re-resolves the tenant. Never set at
/// all for a platform-admin request, by design - see that middleware's class comment.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private Guid _organisationId;
    private string _orgCode = string.Empty;
    private string _schemaName = string.Empty;
    private bool _isSet;

    public Guid OrganisationId => EnsureSet(_organisationId);
    public string OrgCode => EnsureSet(_orgCode);
    public string SchemaName => EnsureSet(_schemaName);

    public void Set(Guid organisationId, string orgCode, string schemaName)
    {
        _organisationId = organisationId;
        _orgCode = orgCode;
        _schemaName = schemaName;
        _isSet = true;
    }

    private T EnsureSet<T>(T value)
    {
        if (!_isSet)
        {
            throw new InvalidOperationException(
                "Tenant context accessed before it was resolved. " +
                "Ensure IdentityResolutionMiddleware runs before any tenant-scoped work " +
                "(a platform-admin token never resolves one - this endpoint may need to be under the PlatformAdmin policy instead).");
        }
        return value;
    }
}
