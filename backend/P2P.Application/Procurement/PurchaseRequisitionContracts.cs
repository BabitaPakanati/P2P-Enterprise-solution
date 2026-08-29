namespace P2P.Application.Procurement;

public sealed record CreateRequisitionLineRequest(
    string ItemDescription, decimal Quantity, string Uom, decimal EstimatedUnitPrice);

public sealed record CreateRequisitionRequest(
    DateOnly RequiredByDate,
    string RequisitionType,
    string Description,
    string Category,
    string Currency,
    string? PreferredSupplierName,
    IReadOnlyList<CreateRequisitionLineRequest> Lines,
    IReadOnlyDictionary<string, string>? CustomFields = null);

/// <summary>Same shape as create - editing a Draft replaces the whole thing, never patches individual fields.</summary>
public sealed record UpdateRequisitionRequest(
    DateOnly RequiredByDate,
    string RequisitionType,
    string Description,
    string Category,
    string Currency,
    string? PreferredSupplierName,
    IReadOnlyList<CreateRequisitionLineRequest> Lines,
    IReadOnlyDictionary<string, string>? CustomFields = null);

/// <summary>Same shape as update, plus the reason an already-approved requisition is being changed.</summary>
public sealed record AmendRequisitionRequest(
    DateOnly RequiredByDate,
    string RequisitionType,
    string Description,
    string Category,
    string Currency,
    string? PreferredSupplierName,
    string ChangeReason,
    IReadOnlyList<CreateRequisitionLineRequest> Lines,
    IReadOnlyDictionary<string, string>? CustomFields = null);

public sealed record RequisitionLineDto(
    Guid Id, int LineNumber, string ItemDescription, decimal Quantity, string Uom, decimal EstimatedUnitPrice, decimal EstimatedValue);

public sealed record RequisitionSummaryDto(
    Guid Id,
    string RequisitionNumber,
    Guid RequesterId,
    DateOnly RequestDate,
    DateOnly RequiredByDate,
    string Category,
    string Description,
    decimal EstimatedValue,
    string Currency,
    string Status);

public sealed record RequisitionDetailDto(
    Guid Id,
    Guid DocumentId,
    string RequisitionNumber,
    Guid RequesterId,
    DateOnly RequestDate,
    DateOnly RequiredByDate,
    string RequisitionType,
    string Description,
    string Category,
    string? PreferredSupplierName,
    decimal EstimatedValue,
    string Currency,
    string Status,
    int CurrentVersionNumber,
    IReadOnlyList<RequisitionLineDto> Lines,
    IReadOnlyDictionary<string, string> CustomFields);

/// <summary>
/// Create → (Update while Draft) → Submit → (workflow decides) → Approved/Rejected
/// → Cancel, matching the "My Requisitions" actions in the requirements doc
/// (§9.1/§9.2). Once Approved, a change goes through AmendAsync instead - same
/// versioned-amendment pattern as PurchaseOrderService (new pending version, old one
/// stays effective until approved), deliberately not available once the
/// requisition is Ordered (a PO already depends on its current values by then -
/// amend the PO instead). Ordered/Closed themselves are driven from the
/// PurchaseOrder side once that module writes back.
/// </summary>
public interface IPurchaseRequisitionService
{
    Task<Guid> CreateAsync(Guid requesterId, CreateRequisitionRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateRequisitionRequest request, CancellationToken ct = default);
    Task SubmitAsync(Guid id, CancellationToken ct = default);
    Task CancelAsync(Guid id, CancellationToken ct = default);
    Task AmendAsync(Guid id, Guid amendedBy, AmendRequisitionRequest request, CancellationToken ct = default);
    Task<RequisitionDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RequisitionSummaryDto>> ListAsync(Guid? requesterId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentVersionDto>> GetVersionHistoryAsync(Guid id, CancellationToken ct = default);
}
