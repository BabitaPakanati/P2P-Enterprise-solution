using P2P.Domain.Common;

namespace P2P.Domain.Procurement;

public enum PurchaseOrderStatus
{
    Draft,
    PendingApproval,
    Approved,
    SentToSupplier,
    Cancelled,
    Closed
}

/// <summary>
/// Same pattern as <see cref="PurchaseRequisition"/>: this row is the *current*
/// version's data; <see cref="DocumentId"/> ties it to the generic version chain so
/// an amendment (see <see cref="AmendmentOf"/>) never overwrites what a prior version
/// looked like - it supersedes it (§76 of the blueprint).
/// </summary>
public sealed class PurchaseOrder : AuditableEntity
{
    public Guid DocumentId { get; set; }
    public string PoNumber { get; set; } = default!;

    public Guid SourceRequisitionId { get; set; }
    public string SupplierName { get; set; } = default!; // placeholder until the Supplier module exists
    public Guid BuyerId { get; set; }

    public DateOnly PoDate { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public string Currency { get; set; } = "USD";

    /// <summary>Denormalised sum of line LineValue - kept in sync by the service.</summary>
    public decimal TotalValue { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    /// <summary>This org's configured extra fields for PurchaseOrder - see PurchaseRequisition.CustomFieldsJson's comment; identical mechanism.</summary>
    public string CustomFieldsJson { get; set; } = "{}";

    private readonly List<PurchaseOrderLine> _lines = new();
    public IReadOnlyCollection<PurchaseOrderLine> Lines => _lines.AsReadOnly();
    public void ReplaceLines(IEnumerable<PurchaseOrderLine> lines)
    {
        _lines.Clear();
        _lines.AddRange(lines);
    }
}

public sealed class PurchaseOrderLine : Entity
{
    public Guid PurchaseOrderId { get; set; }
    public int LineNumber { get; set; }
    public string ItemDescription { get; set; } = default!;
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = default!;
    public decimal UnitPrice { get; set; }

    /// <summary>Computed, not stored - see AppDbContext.OnModelCreating (Ignore).</summary>
    public decimal LineValue => Quantity * UnitPrice;
}
