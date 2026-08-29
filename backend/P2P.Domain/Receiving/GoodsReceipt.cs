using P2P.Domain.Common;

namespace P2P.Domain.Receiving;

public enum GoodsReceiptStatus
{
    Draft,
    Posted,
    Cancelled
}

public enum InspectionStatus
{
    Accepted,
    Rejected,
    PartiallyAccepted
}

/// <summary>
/// Records what physically arrived against a PO (§14 of the blueprint). Same
/// versioning envelope as PurchaseOrder/PurchaseRequisition (Document/DocumentVersion)
/// so a quantity correction after posting never overwrites what was originally
/// recorded - it supersedes it ("Quantity corrections must preserve the previous
/// receipt history"). Unlike PR/PO there is no approval workflow here: Post and
/// Amend both apply immediately - versioning is used purely for the immutable audit
/// trail, not as an approval gate (DocumentVersion.WorkflowInstanceId simply stays
/// null for every GoodsReceipt version).
/// </summary>
public sealed class GoodsReceipt : AuditableEntity
{
    public Guid DocumentId { get; set; }
    public string ReceiptNumber { get; set; } = default!;

    public Guid PurchaseOrderId { get; set; }
    public string PoNumber { get; set; } = default!; // denormalised for worklist display
    public string SupplierName { get; set; } = default!; // denormalised from the PO at receipt time

    public DateOnly DeliveryDate { get; set; }
    public string? DeliveryNoteNumber { get; set; }
    public string? Location { get; set; }

    public GoodsReceiptStatus Status { get; set; } = GoodsReceiptStatus.Draft;

    /// <summary>This org's configured extra fields for GoodsReceipt - identical mechanism to PurchaseOrder.CustomFieldsJson.</summary>
    public string CustomFieldsJson { get; set; } = "{}";

    private readonly List<GoodsReceiptLine> _lines = new();
    public IReadOnlyCollection<GoodsReceiptLine> Lines => _lines.AsReadOnly();
    public void ReplaceLines(IEnumerable<GoodsReceiptLine> lines)
    {
        _lines.Clear();
        _lines.AddRange(lines);
    }
}

public sealed class GoodsReceiptLine : Entity
{
    public Guid GoodsReceiptId { get; set; }
    public Guid PurchaseOrderLineId { get; set; }
    public string ItemDescription { get; set; } = default!; // denormalised from the PO line
    public string Uom { get; set; } = default!;
    public decimal QuantityOrdered { get; set; } // snapshot of the PO line's quantity, for display only
    public decimal QuantityReceived { get; set; }
    public decimal QuantityAccepted { get; set; }
    public decimal QuantityRejected { get; set; }
    public InspectionStatus InspectionStatus { get; set; }
}
