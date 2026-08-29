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
    IReadOnlyList<CreateRequisitionLineRequest> Lines);

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
    IReadOnlyList<RequisitionLineDto> Lines);

/// <summary>
/// Create → Submit → (workflow decides) → Approved/Rejected → Cancel, matching the
/// "My Requisitions" actions in the requirements doc (§9.1/§9.2). Ordered/Closed are
/// driven from the PurchaseOrder side once that module writes back - see
/// PurchaseOrderService.
/// </summary>
public interface IPurchaseRequisitionService
{
    Task<Guid> CreateAsync(Guid requesterId, CreateRequisitionRequest request, CancellationToken ct = default);
    Task SubmitAsync(Guid id, CancellationToken ct = default);
    Task CancelAsync(Guid id, CancellationToken ct = default);
    Task<RequisitionDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RequisitionSummaryDto>> ListAsync(Guid? requesterId, CancellationToken ct = default);
}
