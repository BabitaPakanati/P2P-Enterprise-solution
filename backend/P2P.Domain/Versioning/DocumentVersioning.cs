using P2P.Domain.Common;

namespace P2P.Domain.Versioning;

public enum DocumentVersionStatus
{
    Draft,
    PendingApproval,
    Active,
    Superseded,
    Rejected,
    Cancelled
}

/// <summary>
/// The generic envelope every versionable business document (PR, PO, Contract,
/// Invoice, ...) is tracked through. This entity itself never changes once its
/// business-relevant fields are set; a modification always produces a new
/// <see cref="DocumentVersion"/> and updates <see cref="CurrentVersionId"/> - it
/// never rewrites a prior version. DocumentType is the discriminator a type-specific
/// module (e.g. PurchaseOrder) uses to attach its own strongly-typed version payload.
/// </summary>
public sealed class Document : Entity
{
    public string DocumentNumber { get; set; } = default!;
    public string DocumentType { get; set; } = default!;
    public Guid? CurrentVersionId { get; set; }
    public string CurrentStatus { get; set; } = default!;
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
}

/// <summary>
/// One immutable snapshot in a document's history. Superseding a version never
/// deletes or edits the row that came before it - it links back via
/// <see cref="PreviousVersionId"/> and the old version's status moves to
/// <see cref="DocumentVersionStatus.Superseded"/>, staying queryable forever.
/// </summary>
public sealed class DocumentVersion : Entity
{
    public Guid DocumentId { get; set; }
    public int VersionNumber { get; set; }
    public Guid? PreviousVersionId { get; set; }
    public DocumentVersionStatus VersionStatus { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? ChangeReason { get; set; }
    public string? ChangeComment { get; set; }
    public Guid? WorkflowInstanceId { get; set; }

    /// <summary>
    /// A JSON snapshot of the type-specific fields at the moment this version was
    /// created (e.g. a PO's lines and total at V1). The current version's live data
    /// lives in its own strongly-typed table (e.g. PurchaseOrder) for querying and
    /// worklists; this snapshot is what makes a *superseded* version still fully
    /// inspectable without a bespoke "_history" table per document type - see the
    /// blueprint's PO-amendment acceptance scenario (§76).
    /// </summary>
    public string PayloadJson { get; set; } = "{}";
}
