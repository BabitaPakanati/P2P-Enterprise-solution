using P2P.Domain.Common;

namespace P2P.Domain.Identity;

public sealed class User : AuditableEntity
{
    public string Email { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// ASP.NET Core Identity's PasswordHasher&lt;User&gt; format (PBKDF2, versioned,
    /// self-describing) - never a plain password, never reversible. Null for a user
    /// who hasn't been given a local password yet (e.g. will authenticate via a
    /// future SSO integration instead).
    /// </summary>
    public string? PasswordHash { get; set; }
}

/// <summary>
/// An approval role such as DEPT_MANAGER, FINANCE_DIRECTOR, CFO. Workflow rules and
/// authority assignments are written against roles, never against a specific user -
/// see <see cref="AuthorityAssignment"/>.
/// </summary>
public sealed class Role : Entity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
}

public sealed class Permission : Entity
{
    public string Code { get; set; } = default!;
    public string Description { get; set; } = default!;
}

public sealed class RolePermission : Entity
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
}

public enum AuthorityAssignmentStatus
{
    Active,
    Expired,
    Revoked
}

/// <summary>
/// Grants a user a role's authority over a scope (department/business unit/location)
/// and, optionally, an amount band - for an effective-dated window. A change in
/// authority creates a *new* assignment; it never edits an existing one, so an
/// approval raised under an old assignment continues to resolve correctly even after
/// the authority changes (see the blueprint's authority-change acceptance scenario).
/// </summary>
public sealed class AuthorityAssignment : AuditableEntity
{
    public Guid RoleId { get; set; }
    public Guid UserId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? BusinessUnitId { get; set; }
    public Guid? LocationId { get; set; }
    public decimal? AmountFrom { get; set; }
    public decimal? AmountTo { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public int Priority { get; set; }
    public AuthorityAssignmentStatus Status { get; set; } = AuthorityAssignmentStatus.Active;
}

public enum DelegationStatus
{
    Active,
    Ended,
    Revoked
}

/// <summary>
/// Temporary hand-off of one user's approval authority to another, for a bounded
/// window and (optionally) a specific workflow type. Fully audited on creation and
/// on every approval exercised under it.
/// </summary>
public sealed class Delegation : AuditableEntity
{
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string? WorkflowType { get; set; }
    public string Reason { get; set; } = default!;
    public DelegationStatus Status { get; set; } = DelegationStatus.Active;
}
