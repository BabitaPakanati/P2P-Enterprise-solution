using P2P.Application.Abstractions;

namespace P2P.Infrastructure.MultiTenancy;

/// <summary>Scoped, set once by CurrentUserMiddleware - see TenantContext for the identical pattern.</summary>
public sealed class CurrentUserContext : ICurrentUserContext
{
    private Guid _userId;
    private bool _isSet;

    public Guid UserId
    {
        get
        {
            if (!_isSet)
            {
                throw new InvalidOperationException(
                    "Current user accessed before it was resolved. Ensure CurrentUserMiddleware runs first.");
            }
            return _userId;
        }
    }

    public void Set(Guid userId)
    {
        _userId = userId;
        _isSet = true;
    }
}
