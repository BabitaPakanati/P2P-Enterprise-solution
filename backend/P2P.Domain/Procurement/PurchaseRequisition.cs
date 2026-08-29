using P2P.Domain.Common;

namespace P2P.Domain.Procurement;

public enum PurchaseRequisitionStatus
{
    Draft,
    PendingApproval,
    Approved,
    Rejected,
    Ordered,
    Cancelled
}

/// <summary>
/// The strongly-typed "current state" side of a requisition. Lifecycle bookkeeping
/// (version number, effective dates, workflow linkage) lives in the generic
/// <see cref="P2P.Domain.Versioning.Document"/>/<see cref="P2P.Domain.Versioning.DocumentVersion"/>
/// pair this row points at via <see cref="DocumentId"/> - this table only carries the
/// business fields needed for the worklist and detail screens.
/// </summary>
public sealed class PurchaseRequisition : AuditableEntity
{
    public Guid DocumentId { get; set; }
    public string RequisitionNumber { get; set; } = default!;

    public Guid RequesterId { get; set; }
    public DateOnly RequestDate { get; set; }
    public DateOnly RequiredByDate { get; set; }
    public string RequisitionType { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Category { get; set; } = default!;

    public Guid? DepartmentId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? DeliveryLocationId { get; set; }

    public string? PreferredSupplierName { get; set; }
    public string Currency { get; set; } = "USD";

    /// <summary>Denormalised sum of line EstimatedValue - kept in sync by the service, not the DB, so worklists don't need to join/aggregate lines.</summary>
    public decimal EstimatedValue { get; set; }

    public PurchaseRequisitionStatus Status { get; set; } = PurchaseRequisitionStatus.Draft;

    private readonly List<PurchaseRequisitionLine> _lines = new();
    public IReadOnlyCollection<PurchaseRequisitionLine> Lines => _lines.AsReadOnly();
    public void AddLine(PurchaseRequisitionLine line) => _lines.Add(line);
}

public sealed class PurchaseRequisitionLine : Entity
{
    public Guid PurchaseRequisitionId { get; set; }
    public int LineNumber { get; set; }
    public string ItemDescription { get; set; } = default!;
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = default!;
    public decimal EstimatedUnitPrice { get; set; }

    /// <summary>Computed, not stored - see AppDbContext.OnModelCreating (Ignore).</summary>
    public decimal EstimatedValue => Quantity * EstimatedUnitPrice;
}
