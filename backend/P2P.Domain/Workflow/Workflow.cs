using P2P.Domain.Common;

namespace P2P.Domain.Workflow;

public enum WorkflowDefinitionStatus { Draft, Active, Retired }
public enum WorkflowVersionStatus { Draft, Active, Retired }
public enum RuleOperator { Equals, NotEquals, GreaterThan, LessThan, GreaterOrEqual, LessOrEqual, In, NotIn }
public enum RuleConjunction { And, Or }
public enum WorkflowInstanceStatus { Running, Approved, Rejected, Cancelled }
public enum ApprovalTaskStatus { Pending, Approved, Rejected, Returned, Delegated, Escalated }

/// <summary>
/// One workflow per (organisation-internal) EntityType - e.g. "PurchaseRequisition",
/// "PurchaseOrder", "SupplierBankChange". Never edited in place: changes go through
/// a new <see cref="WorkflowVersion"/> so a transaction already routed under an old
/// version keeps running under it (see the blueprint's workflow-versioning scenario).
/// </summary>
public sealed class WorkflowDefinition : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string? Description { get; set; }
    public WorkflowDefinitionStatus Status { get; set; } = WorkflowDefinitionStatus.Draft;
}

public sealed class WorkflowVersion : AuditableEntity
{
    public Guid WorkflowDefinitionId { get; set; }
    public int VersionNumber { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public WorkflowVersionStatus Status { get; set; } = WorkflowVersionStatus.Draft;
}

public sealed class WorkflowStep : Entity
{
    public Guid WorkflowVersionId { get; set; }
    public string StepCode { get; set; } = default!;
    public string StepName { get; set; } = default!;
    public int Sequence { get; set; }
    public string StepType { get; set; } = default!; // e.g. "Approval", "Notification"
    public bool IsMandatory { get; set; } = true;
    public TimeSpan? Sla { get; set; }
    public Guid ApprovalRoleId { get; set; }
}

/// <summary>
/// One condition on a step, e.g. Amount &gt; 50000. Rules on the same step combine
/// via <see cref="Conjunction"/>; the attribute name is resolved against the business
/// context the workflow engine builds for the transaction (amount, category,
/// supplier risk, business unit, ...), never against hard-coded application code.
/// </summary>
public sealed class WorkflowRule : Entity
{
    public Guid WorkflowStepId { get; set; }
    public string Attribute { get; set; } = default!;
    public RuleOperator Operator { get; set; }
    public string Value { get; set; } = default!;
    public RuleConjunction Conjunction { get; set; } = RuleConjunction.And;
}

/// <summary>
/// A running (or finished) execution of a workflow version against one transaction.
/// </summary>
public sealed class WorkflowInstance : AuditableEntity
{
    public Guid WorkflowVersionId { get; set; }
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public WorkflowInstanceStatus Status { get; set; } = WorkflowInstanceStatus.Running;
}

public sealed class ApprovalTask : AuditableEntity
{
    public Guid WorkflowInstanceId { get; set; }
    public Guid WorkflowStepId { get; set; }
    public Guid AssignedToUserId { get; set; }
    public Guid? DelegatedFromUserId { get; set; }
    public ApprovalTaskStatus Status { get; set; } = ApprovalTaskStatus.Pending;
    public DateTimeOffset? DecidedAtUtc { get; set; }
    public string? Comments { get; set; }
}
