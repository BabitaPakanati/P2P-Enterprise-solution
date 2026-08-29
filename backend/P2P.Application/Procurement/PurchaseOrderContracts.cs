namespace P2P.Application.Procurement;

public sealed record CreateOrderLineRequest(
    string ItemDescription, decimal Quantity, string Uom, decimal UnitPrice);

public sealed record CreatePurchaseOrderRequest(
    Guid SourceRequisitionId,
    string SupplierName,
    DateOnly? DeliveryDate,
    IReadOnlyList<CreateOrderLineRequest> Lines,
    IReadOnlyDictionary<string, string>? CustomFields = null);

/// <summary>Same shape as create - amending a PO always replaces the full line set of the new version, never patches individual fields in place.</summary>
public sealed record AmendPurchaseOrderRequest(
    string SupplierName,
    DateOnly? DeliveryDate,
    string ChangeReason,
    IReadOnlyList<CreateOrderLineRequest> Lines,
    IReadOnlyDictionary<string, string>? CustomFields = null);

public sealed record OrderLineDto(
    Guid Id, int LineNumber, string ItemDescription, decimal Quantity, string Uom, decimal UnitPrice, decimal LineValue);

public sealed record OrderSummaryDto(
    Guid Id,
    string PoNumber,
    string SupplierName,
    DateOnly PoDate,
    DateOnly? DeliveryDate,
    decimal TotalValue,
    string Currency,
    string Status);

public sealed record OrderDetailDto(
    Guid Id,
    Guid DocumentId,
    string PoNumber,
    Guid SourceRequisitionId,
    string SupplierName,
    Guid BuyerId,
    DateOnly PoDate,
    DateOnly? DeliveryDate,
    decimal TotalValue,
    string Currency,
    string Status,
    int CurrentVersionNumber,
    IReadOnlyList<OrderLineDto> Lines,
    IReadOnlyDictionary<string, string> CustomFields);

public sealed record DocumentVersionDto(
    Guid Id,
    int VersionNumber,
    string VersionStatus,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? ChangeReason,
    string? ChangeComment,
    string PayloadJson);

/// <summary>
/// Create (from an approved requisition) → Submit → (workflow decides) → Send to
/// supplier; Amend always creates a new version - see §13's PO-amendment diagram.
/// The prior version is never edited, only superseded (VersionStatus.Superseded)
/// and still returned by GetVersionHistoryAsync.
/// </summary>
public interface IPurchaseOrderService
{
    Task<Guid> CreateFromRequisitionAsync(Guid buyerId, CreatePurchaseOrderRequest request, CancellationToken ct = default);
    Task SubmitAsync(Guid id, CancellationToken ct = default);
    Task SendToSupplierAsync(Guid id, CancellationToken ct = default);
    Task AmendAsync(Guid id, Guid amendedBy, AmendPurchaseOrderRequest request, CancellationToken ct = default);
    Task<OrderDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<OrderSummaryDto>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DocumentVersionDto>> GetVersionHistoryAsync(Guid id, CancellationToken ct = default);
}
