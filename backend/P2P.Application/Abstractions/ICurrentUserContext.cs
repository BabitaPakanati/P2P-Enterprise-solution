namespace P2P.Application.Abstractions;

/// <summary>
/// The acting user for the current request - who "CreatedBy", "SubmittedBy", the
/// audit log's UserId, and approval-task assignment all resolve against. Stands in
/// for a real JWT claim today, resolved from an X-User-Id header by
/// CurrentUserMiddleware, exactly parallel to how ITenantContext stands in for a
/// real org claim - see docs/ARCHITECTURE.md.
/// </summary>
public interface ICurrentUserContext
{
    Guid UserId { get; }
}
