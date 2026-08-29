using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using P2P.Application.Abstractions;
using P2P.Application.Configuration;
using P2P.Application.Procurement;
using P2P.Application.Workflow;
using P2P.Domain.Audit;
using P2P.Domain.Procurement;
using P2P.Domain.Versioning;
using P2P.Domain.Workflow;
using P2P.Infrastructure.Persistence;

namespace P2P.Infrastructure.Procurement;

public sealed class PurchaseOrderService : IPurchaseOrderService, IWorkflowCompletionHandler
{
    private readonly AppDbContext _db;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ICurrentUserContext _currentUser;
    private readonly ICustomFieldValidator _customFields;

    public string EntityType => "PurchaseOrder";

    public PurchaseOrderService(AppDbContext db, IWorkflowEngine workflowEngine, ICurrentUserContext currentUser, ICustomFieldValidator customFields)
    {
        _db = db;
        _workflowEngine = workflowEngine;
        _currentUser = currentUser;
        _customFields = customFields;
    }

    public async Task<Guid> CreateFromRequisitionAsync(Guid buyerId, CreatePurchaseOrderRequest request, CancellationToken ct = default)
    {
        var pr = await _db.PurchaseRequisitions.FindAsync([request.SourceRequisitionId], ct)
            ?? throw new InvalidOperationException("Source requisition not found.");
        if (pr.Status != PurchaseRequisitionStatus.Approved)
        {
            throw new InvalidOperationException($"Only an Approved requisition can be converted to a PO (current status: {pr.Status}).");
        }
        if (request.Lines.Count == 0)
        {
            throw new InvalidOperationException("A purchase order needs at least one line.");
        }

        var number = await NextNumberAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var document = new Document
        {
            DocumentNumber = number,
            DocumentType = EntityType,
            CurrentStatus = nameof(PurchaseOrderStatus.Draft),
            CreatedBy = buyerId,
            CreatedAtUtc = now
        };
        var version = new DocumentVersion
        {
            DocumentId = document.Id,
            VersionNumber = 1,
            VersionStatus = DocumentVersionStatus.Draft,
            EffectiveFrom = now,
            CreatedBy = buyerId,
            CreatedAtUtc = now
        };
        document.CurrentVersionId = version.Id;

        var po = new PurchaseOrder
        {
            DocumentId = document.Id,
            PoNumber = number,
            SourceRequisitionId = pr.Id,
            SupplierName = request.SupplierName,
            BuyerId = buyerId,
            PoDate = DateOnly.FromDateTime(now.UtcDateTime),
            DeliveryDate = request.DeliveryDate,
            Currency = pr.Currency,
            Status = PurchaseOrderStatus.Draft,
            CreatedBy = buyerId,
            CreatedAtUtc = now
        };
        po.ReplaceLines(request.Lines.Select((l, i) => new PurchaseOrderLine
        {
            PurchaseOrderId = po.Id,
            LineNumber = i + 1,
            ItemDescription = l.ItemDescription,
            Quantity = l.Quantity,
            Uom = l.Uom,
            UnitPrice = l.UnitPrice
        }));
        po.TotalValue = po.Lines.Sum(l => l.LineValue);
        po.CustomFieldsJson = await _customFields.ValidateAndSerializeAsync(EntityType, request.CustomFields, ct);
        version.PayloadJson = Snapshot(po);

        pr.Status = PurchaseRequisitionStatus.Ordered;
        pr.UpdatedBy = buyerId;
        pr.UpdatedAtUtc = now;

        _db.Documents.Add(document);
        _db.DocumentVersions.Add(version);
        _db.PurchaseOrders.Add(po);
        _db.AuditLogs.Add(AuditLog.Create(EntityType, po.Id, "CREATE", buyerId, await UserNameAsync(buyerId, ct), version.Id, source: "API",
            comments: $"Generated from requisition {pr.RequisitionNumber}"));

        await _db.SaveChangesAsync(ct);
        return po.Id;
    }

    public async Task SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var po = await _db.PurchaseOrders.FindAsync([id], ct) ?? throw new InvalidOperationException("Purchase order not found.");
        if (po.Status != PurchaseOrderStatus.Draft)
        {
            throw new InvalidOperationException($"Only a Draft PO can be submitted (current status: {po.Status}).");
        }

        var document = (await _db.Documents.FindAsync([po.DocumentId], ct))!;
        var version = (await _db.DocumentVersions.FindAsync([document.CurrentVersionId!.Value], ct))!;

        // See PurchaseRequisitionService.SubmitAsync's comment: one transaction
        // spanning this and WorkflowEngine.StartAsync's own SaveChanges, so a
        // workflow that can't resolve an approver rolls the PO back to Draft
        // instead of leaving it PendingApproval with no approval task.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        po.Status = PurchaseOrderStatus.PendingApproval;
        po.UpdatedBy = _currentUser.UserId;
        po.UpdatedAtUtc = DateTimeOffset.UtcNow;
        document.CurrentStatus = nameof(PurchaseOrderStatus.PendingApproval);
        version.VersionStatus = DocumentVersionStatus.PendingApproval;

        _db.AuditLogs.Add(AuditLog.Create(EntityType, po.Id, "SUBMIT", _currentUser.UserId, await UserNameAsync(_currentUser.UserId, ct), version.Id, source: "API"));
        await _db.SaveChangesAsync(ct);

        var instanceId = await _workflowEngine.StartAsync(EntityType, po.Id, version.Id, new Dictionary<string, decimal> { ["Amount"] = po.TotalValue }, ct);
        version.WorkflowInstanceId = instanceId;
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task SendToSupplierAsync(Guid id, CancellationToken ct = default)
    {
        var po = await _db.PurchaseOrders.FindAsync([id], ct) ?? throw new InvalidOperationException("Purchase order not found.");
        if (po.Status != PurchaseOrderStatus.Approved)
        {
            throw new InvalidOperationException($"Only an Approved PO can be sent to the supplier (current status: {po.Status}).");
        }
        po.Status = PurchaseOrderStatus.SentToSupplier;
        po.UpdatedBy = _currentUser.UserId;
        po.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _db.AuditLogs.Add(AuditLog.Create(EntityType, po.Id, "SEND_TO_SUPPLIER", _currentUser.UserId, await UserNameAsync(_currentUser.UserId, ct), source: "API"));
        await _db.SaveChangesAsync(ct);
    }

    public async Task AmendAsync(Guid id, Guid amendedBy, AmendPurchaseOrderRequest request, CancellationToken ct = default)
    {
        var po = await _db.PurchaseOrders.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new InvalidOperationException("Purchase order not found.");
        if (po.Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.SentToSupplier))
        {
            throw new InvalidOperationException($"A PO in status '{po.Status}' cannot be amended - it must be Approved or SentToSupplier.");
        }
        if (request.Lines.Count == 0)
        {
            throw new InvalidOperationException("An amendment needs at least one line.");
        }
        if (string.IsNullOrWhiteSpace(request.ChangeReason))
        {
            throw new InvalidOperationException("A change reason is required for a PO amendment.");
        }

        var document = (await _db.Documents.FindAsync([po.DocumentId], ct))!;
        var currentVersion = (await _db.DocumentVersions.FindAsync([document.CurrentVersionId!.Value], ct))!;
        var now = DateTimeOffset.UtcNow;

        var proposedTotal = request.Lines.Sum(l => l.Quantity * l.UnitPrice);
        var proposedCustomFields = await _customFields.ValidateAndSerializeAsync(EntityType, request.CustomFields, ct);
        var newVersion = new DocumentVersion
        {
            DocumentId = document.Id,
            VersionNumber = currentVersion.VersionNumber + 1,
            PreviousVersionId = currentVersion.Id,
            VersionStatus = DocumentVersionStatus.PendingApproval,
            EffectiveFrom = now,
            CreatedBy = amendedBy,
            CreatedAtUtc = now,
            ChangeReason = request.ChangeReason,
            PayloadJson = JsonSerializer.Serialize(new PoSnapshot(
                request.SupplierName, request.DeliveryDate, proposedTotal, po.Currency,
                request.Lines.Select(l => new PoSnapshotLine(l.ItemDescription, l.Quantity, l.Uom, l.UnitPrice)).ToList(),
                proposedCustomFields))
        };

        // The V1 (current) version is untouched and stays Active/effective - the
        // proposed change lives only in newVersion until it's approved. This is the
        // §76 guarantee: "the system must never replace V1."
        document.CurrentStatus = nameof(DocumentVersionStatus.PendingApproval);

        // See PurchaseRequisitionService.SubmitAsync's comment on why this is one transaction.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        _db.DocumentVersions.Add(newVersion);
        _db.AuditLogs.Add(AuditLog.Create(EntityType, po.Id, "SUBMIT", amendedBy, await UserNameAsync(amendedBy, ct), newVersion.Id, source: "API", reason: request.ChangeReason,
            comments: $"Amendment: v{currentVersion.VersionNumber} -> v{newVersion.VersionNumber}"));
        await _db.SaveChangesAsync(ct);

        var instanceId = await _workflowEngine.StartAsync(EntityType, po.Id, newVersion.Id, new Dictionary<string, decimal> { ["Amount"] = proposedTotal }, ct);
        newVersion.WorkflowInstanceId = instanceId;
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<OrderDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var po = await _db.PurchaseOrders.Include(p => p.Lines).AsSplitQuery().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (po is null) return null;

        var document = await _db.Documents.FindAsync([po.DocumentId], ct);
        var versionNumber = document?.CurrentVersionId is Guid vId
            ? await _db.DocumentVersions.Where(v => v.Id == vId).Select(v => v.VersionNumber).FirstOrDefaultAsync(ct)
            : 1;

        return new OrderDetailDto(
            po.Id, po.DocumentId, po.PoNumber, po.SourceRequisitionId, po.SupplierName, po.BuyerId,
            po.PoDate, po.DeliveryDate, po.TotalValue, po.Currency, po.Status.ToString(), versionNumber,
            po.Lines.OrderBy(l => l.LineNumber).Select(l => new OrderLineDto(l.Id, l.LineNumber, l.ItemDescription, l.Quantity, l.Uom, l.UnitPrice, l.LineValue)).ToList(),
            DeserializeCustomFields(po.CustomFieldsJson));
    }

    public async Task<IReadOnlyList<OrderSummaryDto>> ListAsync(CancellationToken ct = default)
        => await _db.PurchaseOrders
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new OrderSummaryDto(p.Id, p.PoNumber, p.SupplierName, p.PoDate, p.DeliveryDate, p.TotalValue, p.Currency, p.Status.ToString()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentVersionDto>> GetVersionHistoryAsync(Guid id, CancellationToken ct = default)
    {
        var po = await _db.PurchaseOrders.FindAsync([id], ct) ?? throw new InvalidOperationException("Purchase order not found.");
        return await _db.DocumentVersions
            .Where(v => v.DocumentId == po.DocumentId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new DocumentVersionDto(v.Id, v.VersionNumber, v.VersionStatus.ToString(), v.EffectiveFrom, v.EffectiveTo, v.ChangeReason, v.ChangeComment, v.PayloadJson))
            .ToListAsync(ct);
    }

    // --- IWorkflowCompletionHandler ------------------------------------------------

    public async Task OnApprovedAsync(Guid entityId, Guid documentVersionId, CancellationToken ct = default)
    {
        var po = await _db.PurchaseOrders.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == entityId, ct);
        var version = await _db.DocumentVersions.FindAsync([documentVersionId], ct);
        if (po is null || version is null) return;
        var document = await _db.Documents.FindAsync([po.DocumentId], ct);

        if (version.VersionNumber == 1)
        {
            po.Status = PurchaseOrderStatus.Approved;
        }
        else
        {
            // An amendment: apply the proposed snapshot onto the live row now that
            // it's approved, and only now supersede the version that was effective
            // until this moment.
            var snapshot = JsonSerializer.Deserialize<PoSnapshot>(version.PayloadJson)!;
            po.SupplierName = snapshot.SupplierName;
            po.DeliveryDate = snapshot.DeliveryDate;
            po.TotalValue = snapshot.TotalValue;
            po.CustomFieldsJson = snapshot.CustomFieldsJson;

            // Mutating the tracked entity's backing collection directly (via
            // ReplaceLines) and letting SaveChanges snapshot-diff it turned out to be
            // unreliable here - EF Core threw a concurrency exception trying to
            // delete the old line, because the read-only Lines property wraps the
            // field in a fresh ReadOnlyCollection on every access instead of
            // returning one stable reference for the tracker to diff against.
            // Explicit Remove/Add is unambiguous and is what EF actually needs.
            var newLines = snapshot.Lines.Select((l, i) => new PurchaseOrderLine
            {
                PurchaseOrderId = po.Id, LineNumber = i + 1, ItemDescription = l.ItemDescription, Quantity = l.Quantity, Uom = l.Uom, UnitPrice = l.UnitPrice
            }).ToList();
            _db.PurchaseOrderLines.RemoveRange(po.Lines);
            _db.PurchaseOrderLines.AddRange(newLines);
            po.ReplaceLines(newLines);

            if (document?.CurrentVersionId is Guid previousVersionId)
            {
                var previous = await _db.DocumentVersions.FindAsync([previousVersionId], ct);
                if (previous is not null)
                {
                    previous.VersionStatus = DocumentVersionStatus.Superseded;
                    previous.EffectiveTo = DateTimeOffset.UtcNow;
                }
            }
        }

        po.UpdatedBy = version.CreatedBy;
        po.UpdatedAtUtc = DateTimeOffset.UtcNow;
        version.VersionStatus = DocumentVersionStatus.Active;
        if (document is not null)
        {
            document.CurrentVersionId = version.Id;
            document.CurrentStatus = po.Status.ToString();
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task OnRejectedAsync(Guid entityId, Guid documentVersionId, string? reason, CancellationToken ct = default)
    {
        var po = await _db.PurchaseOrders.FindAsync([entityId], ct);
        var version = await _db.DocumentVersions.FindAsync([documentVersionId], ct);
        if (po is null || version is null) return;
        var document = await _db.Documents.FindAsync([po.DocumentId], ct);

        version.VersionStatus = DocumentVersionStatus.Rejected;
        version.ChangeReason ??= reason;

        if (version.VersionNumber == 1)
        {
            po.Status = PurchaseOrderStatus.Cancelled; // a PO that never got its first approval has nothing to fall back to
        }
        // else: an amendment was rejected - V1 (or whichever version was already
        // Active) was never touched and remains the effective one; po's live fields
        // are untouched too, by construction (see AmendAsync's comment).

        if (document is not null) document.CurrentStatus = po.Status.ToString();
        await _db.SaveChangesAsync(ct);
    }

    // --- helpers ---------------------------------------------------------------------

    private async Task<string> NextNumberAsync(CancellationToken ct)
    {
        var count = await _db.Documents.CountAsync(d => d.DocumentType == EntityType, ct);
        return $"PO-{count + 1:D5}";
    }

    private async Task<string> UserNameAsync(Guid userId, CancellationToken ct)
        => await _db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? userId.ToString();

    private static string Snapshot(PurchaseOrder po) => JsonSerializer.Serialize(new PoSnapshot(
        po.SupplierName, po.DeliveryDate, po.TotalValue, po.Currency,
        po.Lines.Select(l => new PoSnapshotLine(l.ItemDescription, l.Quantity, l.Uom, l.UnitPrice)).ToList(), po.CustomFieldsJson));

    private static IReadOnlyDictionary<string, string> DeserializeCustomFields(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

    private sealed record PoSnapshot(string SupplierName, DateOnly? DeliveryDate, decimal TotalValue, string Currency, List<PoSnapshotLine> Lines, string CustomFieldsJson);
    private sealed record PoSnapshotLine(string ItemDescription, decimal Quantity, string Uom, decimal UnitPrice);
}
