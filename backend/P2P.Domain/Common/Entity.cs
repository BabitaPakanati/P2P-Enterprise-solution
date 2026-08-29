namespace P2P.Domain.Common;

/// <summary>
/// Base type for every entity in the domain. Identity is a server-generated GUID so
/// document numbers (human-facing, e.g. "PR-10045") can remain a separate, sequential
/// concern from the primary key.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

/// <summary>
/// Entities that must record who created/last touched them and when (UTC).
/// This is the minimum audit footprint required on every transactional row by
/// the platform's non-negotiable audit principle - it is not a substitute for
/// the append-only AuditLog, which records the full field-level history.
/// </summary>
public abstract class AuditableEntity : Entity
{
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

/// <summary>
/// Organisational ownership for a record, scoped *within* one organisation's schema.
/// OrganisationId is deliberately absent here: under schema-per-organisation tenancy
/// the organisation is the schema itself, so every row in this schema already belongs
/// to exactly one org without a column to enforce (or forget to filter by).
/// </summary>
public sealed record OrgOwnership(
    Guid? LegalEntityId,
    Guid? BusinessUnitId,
    Guid? DepartmentId,
    Guid? LocationId);
