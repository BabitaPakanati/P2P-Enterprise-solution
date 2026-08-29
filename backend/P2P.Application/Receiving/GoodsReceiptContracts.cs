using P2P.Application.Procurement;

namespace P2P.Application.Receiving;

/// <summary>QuantityAccepted is never submitted - it's always derived as QuantityReceived - QuantityRejected (see GoodsReceiptService.BuildLinesAsync).</summary>
public sealed record CreateGoodsReceiptLineRequest(
    Guid PurchaseOrderLineId, decimal QuantityReceived, decimal QuantityRejected);

public sealed record CreateGoodsReceiptRequest(
    Guid PurchaseOrderId,
    DateOnly DeliveryDate,
    string? DeliveryNoteNumber,
    string? Location,
    IReadOnlyList<CreateGoodsReceiptLineRequest> Lines,
    IReadOnlyDictionary<string, string>? CustomFields = null);

/// <summary>Same shape as create - editing a Draft replaces the whole line set, no new version.</summary>
public sealed record UpdateGoodsReceiptRequest(
    DateOnly DeliveryDate,
    string? DeliveryNoteNumber,
    string? Location,
    IReadOnlyList<CreateGoodsReceiptLineRequest> Lines,
    IReadOnlyDictionary<string, string>? CustomFields = null);

/// <summary>Correcting a Posted receipt always creates a new version - see GoodsReceipt's summary comment.</summary>
public sealed record AmendGoodsReceiptRequest(
    DateOnly DeliveryDate,
    string? DeliveryNoteNumber,
    string? Location,
    string ChangeReason,
    IReadOnlyList<CreateGoodsReceiptLineRequest> Lines,
    IReadOnlyDictionary<string, string>? CustomFields = null);

public sealed record GoodsReceiptLineDto(
    Guid Id, Guid PurchaseOrderLineId, string ItemDescription, string Uom,
    decimal QuantityOrdered, decimal QuantityReceived, decimal QuantityAccepted, decimal QuantityRejected, string InspectionStatus);

public sealed record GoodsReceiptSummaryDto(
    Guid Id, string ReceiptNumber, Guid PurchaseOrderId, string PoNumber, string SupplierName,
    DateOnly DeliveryDate, string Status);

public sealed record GoodsReceiptDetailDto(
    Guid Id,
    Guid DocumentId,
    string ReceiptNumber,
    Guid PurchaseOrderId,
    string PoNumber,
    string SupplierName,
    DateOnly DeliveryDate,
    string? DeliveryNoteNumber,
    string? Location,
    string Status,
    int CurrentVersionNumber,
    IReadOnlyList<GoodsReceiptLineDto> Lines,
    IReadOnlyDictionary<string, string> CustomFields);

/// <summary>What's left to receive on one PO line - the Create-GR form's starting point.</summary>
public sealed record ReceivableLineDto(
    Guid PurchaseOrderLineId, string ItemDescription, string Uom, decimal QuantityOrdered, decimal QuantityAlreadyReceived, decimal QuantityRemaining);

/// <summary>
/// The PO's own receipt progress. Deliberately not a value on PurchaseOrder.Status -
/// receiving is its own status dimension, separate from the PO's approval/lifecycle
/// status (§47: "the system must distinguish Business Status / Workflow Status /
/// Exception Status ... do not use one generic Status field for all purposes").
/// </summary>
public sealed record PurchaseOrderReceiptStatusDto(string ReceiptStatus, IReadOnlyList<ReceivableLineDto> Lines);

/// <summary>
/// Create (against an Approved/SentToSupplier PO, Draft) → Post (finalises, no
/// approval needed) → Amend (a correction after posting - always versioned, applies
/// immediately). See GoodsReceipt's class summary for why there's no workflow step.
/// </summary>
public interface IGoodsReceiptService
{
    Task<Guid> CreateAsync(Guid recordedBy, CreateGoodsReceiptRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateGoodsReceiptRequest request, CancellationToken ct = default);
    Task PostAsync(Guid id, CancellationToken ct = default);
    Task CancelAsync(Guid id, CancellationToken ct = default);
    Task AmendAsync(Guid id, Guid amendedBy, AmendGoodsReceiptRequest request, CancellationToken ct = default);
    Task<GoodsReceiptDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<GoodsReceiptSummaryDto>> ListAsync(Guid? purchaseOrderId = null, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentVersionDto>> GetVersionHistoryAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseOrderReceiptStatusDto> GetReceiptStatusAsync(Guid purchaseOrderId, CancellationToken ct = default);
}
