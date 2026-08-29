using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using P2P.Application.Abstractions;
using P2P.Application.Configuration;
using P2P.Application.Procurement;
using P2P.Application.Receiving;
using P2P.Domain.Audit;
using P2P.Domain.Procurement;
using P2P.Domain.Receiving;
using P2P.Domain.Versioning;
using P2P.Infrastructure.Persistence;

namespace P2P.Infrastructure.Receiving;

public sealed class GoodsReceiptService : IGoodsReceiptService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly ICustomFieldValidator _customFields;

    public const string EntityType = "GoodsReceipt";

    public GoodsReceiptService(AppDbContext db, ICurrentUserContext currentUser, ICustomFieldValidator customFields)
    {
        _db = db;
        _currentUser = currentUser;
        _customFields = customFields;
    }

    public async Task<Guid> CreateAsync(Guid recordedBy, CreateGoodsReceiptRequest request, CancellationToken ct = default)
    {
        var po = await _db.PurchaseOrders.FindAsync([request.PurchaseOrderId], ct)
            ?? throw new InvalidOperationException("Purchase order not found.");
        if (po.Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.SentToSupplier))
        {
            throw new InvalidOperationException($"Goods can only be received against an Approved or SentToSupplier PO (current status: {po.Status}).");
        }

        var lines = await BuildLinesAsync(po.Id, request.Lines, excludeGoodsReceiptId: null, ct);
        var number = await NextNumberAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var document = new Document
        {
            DocumentNumber = number,
            DocumentType = EntityType,
            CurrentStatus = nameof(GoodsReceiptStatus.Draft),
            CreatedBy = recordedBy,
            CreatedAtUtc = now
        };
        var version = new DocumentVersion
        {
            DocumentId = document.Id,
            VersionNumber = 1,
            VersionStatus = DocumentVersionStatus.Draft,
            EffectiveFrom = now,
            CreatedBy = recordedBy,
            CreatedAtUtc = now
        };
        document.CurrentVersionId = version.Id;

        var gr = new GoodsReceipt
        {
            DocumentId = document.Id,
            ReceiptNumber = number,
            PurchaseOrderId = po.Id,
            PoNumber = po.PoNumber,
            SupplierName = po.SupplierName,
            DeliveryDate = request.DeliveryDate,
            DeliveryNoteNumber = request.DeliveryNoteNumber,
            Location = request.Location,
            Status = GoodsReceiptStatus.Draft,
            CreatedBy = recordedBy,
            CreatedAtUtc = now
        };
        foreach (var line in lines) line.GoodsReceiptId = gr.Id;
        gr.ReplaceLines(lines);
        gr.CustomFieldsJson = await _customFields.ValidateAndSerializeAsync(EntityType, request.CustomFields, ct);
        version.PayloadJson = Snapshot(gr);

        _db.Documents.Add(document);
        _db.DocumentVersions.Add(version);
        _db.GoodsReceipts.Add(gr);
        _db.AuditLogs.Add(AuditLog.Create(EntityType, gr.Id, "CREATE", recordedBy, await UserNameAsync(recordedBy, ct), version.Id, source: "API",
            comments: $"Against PO {po.PoNumber}"));

        await _db.SaveChangesAsync(ct);
        return gr.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateGoodsReceiptRequest request, CancellationToken ct = default)
    {
        var gr = await _db.GoodsReceipts.Include(g => g.Lines).FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new InvalidOperationException("Goods receipt not found.");
        if (gr.Status != GoodsReceiptStatus.Draft)
        {
            throw new InvalidOperationException($"Only a Draft goods receipt can be edited (current status: {gr.Status}).");
        }

        var lines = await BuildLinesAsync(gr.PurchaseOrderId, request.Lines, excludeGoodsReceiptId: null, ct);

        gr.DeliveryDate = request.DeliveryDate;
        gr.DeliveryNoteNumber = request.DeliveryNoteNumber;
        gr.Location = request.Location;
        gr.CustomFieldsJson = await _customFields.ValidateAndSerializeAsync(EntityType, request.CustomFields, ct);
        gr.UpdatedBy = _currentUser.UserId;
        gr.UpdatedAtUtc = DateTimeOffset.UtcNow;

        // See PurchaseOrderService.OnApprovedAsync's comment: explicit RemoveRange/
        // AddRange rather than relying on ReplaceLines alone for SaveChanges diffing.
        _db.GoodsReceiptLines.RemoveRange(gr.Lines);
        foreach (var line in lines) line.GoodsReceiptId = gr.Id;
        _db.GoodsReceiptLines.AddRange(lines);
        gr.ReplaceLines(lines);

        var document = (await _db.Documents.FindAsync([gr.DocumentId], ct))!;
        var version = (await _db.DocumentVersions.FindAsync([document.CurrentVersionId!.Value], ct))!;
        version.PayloadJson = Snapshot(gr);

        _db.AuditLogs.Add(AuditLog.Create(EntityType, gr.Id, "UPDATE", _currentUser.UserId, await UserNameAsync(_currentUser.UserId, ct), version.Id, source: "API"));
        await _db.SaveChangesAsync(ct);
    }

    public async Task PostAsync(Guid id, CancellationToken ct = default)
    {
        var gr = await _db.GoodsReceipts.FindAsync([id], ct) ?? throw new InvalidOperationException("Goods receipt not found.");
        if (gr.Status != GoodsReceiptStatus.Draft)
        {
            throw new InvalidOperationException($"Only a Draft goods receipt can be posted (current status: {gr.Status}).");
        }

        gr.Status = GoodsReceiptStatus.Posted;
        gr.UpdatedBy = _currentUser.UserId;
        gr.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var document = (await _db.Documents.FindAsync([gr.DocumentId], ct))!;
        var version = (await _db.DocumentVersions.FindAsync([document.CurrentVersionId!.Value], ct))!;
        document.CurrentStatus = nameof(GoodsReceiptStatus.Posted);
        version.VersionStatus = DocumentVersionStatus.Active;

        _db.AuditLogs.Add(AuditLog.Create(EntityType, gr.Id, "POST", _currentUser.UserId, await UserNameAsync(_currentUser.UserId, ct), version.Id, source: "API"));
        await _db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(Guid id, CancellationToken ct = default)
    {
        var gr = await _db.GoodsReceipts.FindAsync([id], ct) ?? throw new InvalidOperationException("Goods receipt not found.");
        if (gr.Status != GoodsReceiptStatus.Draft)
        {
            throw new InvalidOperationException($"Only a Draft goods receipt can be cancelled (current status: {gr.Status}).");
        }

        gr.Status = GoodsReceiptStatus.Cancelled;
        gr.UpdatedBy = _currentUser.UserId;
        gr.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var document = (await _db.Documents.FindAsync([gr.DocumentId], ct))!;
        document.CurrentStatus = nameof(GoodsReceiptStatus.Cancelled);

        _db.AuditLogs.Add(AuditLog.Create(EntityType, gr.Id, "CANCEL", _currentUser.UserId, await UserNameAsync(_currentUser.UserId, ct), source: "API"));
        await _db.SaveChangesAsync(ct);
    }

    public async Task AmendAsync(Guid id, Guid amendedBy, AmendGoodsReceiptRequest request, CancellationToken ct = default)
    {
        var gr = await _db.GoodsReceipts.Include(g => g.Lines).FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new InvalidOperationException("Goods receipt not found.");
        if (gr.Status != GoodsReceiptStatus.Posted)
        {
            throw new InvalidOperationException($"Only a Posted goods receipt can be corrected (current status: {gr.Status}).");
        }
        if (string.IsNullOrWhiteSpace(request.ChangeReason))
        {
            throw new InvalidOperationException("A change reason is required to correct a goods receipt.");
        }

        // Exclude this GR's own already-posted quantities from the "remaining on the
        // PO" check - the new lines are replacing them, not adding on top of them.
        var lines = await BuildLinesAsync(gr.PurchaseOrderId, request.Lines, excludeGoodsReceiptId: gr.Id, ct);

        var document = (await _db.Documents.FindAsync([gr.DocumentId], ct))!;
        var currentVersion = (await _db.DocumentVersions.FindAsync([document.CurrentVersionId!.Value], ct))!;
        var now = DateTimeOffset.UtcNow;

        gr.DeliveryDate = request.DeliveryDate;
        gr.DeliveryNoteNumber = request.DeliveryNoteNumber;
        gr.Location = request.Location;
        gr.CustomFieldsJson = await _customFields.ValidateAndSerializeAsync(EntityType, request.CustomFields, ct);
        gr.UpdatedBy = amendedBy;
        gr.UpdatedAtUtc = now;

        _db.GoodsReceiptLines.RemoveRange(gr.Lines);
        foreach (var line in lines) line.GoodsReceiptId = gr.Id;
        _db.GoodsReceiptLines.AddRange(lines);
        gr.ReplaceLines(lines);

        // The prior version is never edited - it's superseded, and the new one
        // becomes Active immediately (no approval gate; see GoodsReceipt's summary).
        currentVersion.VersionStatus = DocumentVersionStatus.Superseded;
        currentVersion.EffectiveTo = now;

        var newVersion = new DocumentVersion
        {
            DocumentId = document.Id,
            VersionNumber = currentVersion.VersionNumber + 1,
            PreviousVersionId = currentVersion.Id,
            VersionStatus = DocumentVersionStatus.Active,
            EffectiveFrom = now,
            CreatedBy = amendedBy,
            CreatedAtUtc = now,
            ChangeReason = request.ChangeReason,
            PayloadJson = Snapshot(gr)
        };
        document.CurrentVersionId = newVersion.Id;

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        _db.DocumentVersions.Add(newVersion);
        _db.AuditLogs.Add(AuditLog.Create(EntityType, gr.Id, "CORRECT", amendedBy, await UserNameAsync(amendedBy, ct), newVersion.Id, source: "API", reason: request.ChangeReason,
            comments: $"Correction: v{currentVersion.VersionNumber} -> v{newVersion.VersionNumber}"));
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<GoodsReceiptDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var gr = await _db.GoodsReceipts.Include(g => g.Lines).AsSplitQuery().FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gr is null) return null;

        var document = await _db.Documents.FindAsync([gr.DocumentId], ct);
        var versionNumber = document?.CurrentVersionId is Guid vId
            ? await _db.DocumentVersions.Where(v => v.Id == vId).Select(v => v.VersionNumber).FirstOrDefaultAsync(ct)
            : 1;

        return new GoodsReceiptDetailDto(
            gr.Id, gr.DocumentId, gr.ReceiptNumber, gr.PurchaseOrderId, gr.PoNumber, gr.SupplierName,
            gr.DeliveryDate, gr.DeliveryNoteNumber, gr.Location, gr.Status.ToString(), versionNumber,
            gr.Lines.Select(l => new GoodsReceiptLineDto(
                l.Id, l.PurchaseOrderLineId, l.ItemDescription, l.Uom, l.QuantityOrdered, l.QuantityReceived, l.QuantityAccepted, l.QuantityRejected, l.InspectionStatus.ToString())).ToList(),
            DeserializeCustomFields(gr.CustomFieldsJson));
    }

    public async Task<IReadOnlyList<GoodsReceiptSummaryDto>> ListAsync(Guid? purchaseOrderId = null, CancellationToken ct = default)
    {
        var query = _db.GoodsReceipts.AsQueryable();
        if (purchaseOrderId is Guid poId) query = query.Where(g => g.PurchaseOrderId == poId);
        return await query
            .OrderByDescending(g => g.CreatedAtUtc)
            .Select(g => new GoodsReceiptSummaryDto(g.Id, g.ReceiptNumber, g.PurchaseOrderId, g.PoNumber, g.SupplierName, g.DeliveryDate, g.Status.ToString()))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentVersionDto>> GetVersionHistoryAsync(Guid id, CancellationToken ct = default)
    {
        var gr = await _db.GoodsReceipts.FindAsync([id], ct) ?? throw new InvalidOperationException("Goods receipt not found.");
        return await _db.DocumentVersions
            .Where(v => v.DocumentId == gr.DocumentId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new DocumentVersionDto(v.Id, v.VersionNumber, v.VersionStatus.ToString(), v.EffectiveFrom, v.EffectiveTo, v.ChangeReason, v.ChangeComment, v.PayloadJson))
            .ToListAsync(ct);
    }

    public async Task<PurchaseOrderReceiptStatusDto> GetReceiptStatusAsync(Guid purchaseOrderId, CancellationToken ct = default)
    {
        var poLines = await _db.PurchaseOrderLines.Where(l => l.PurchaseOrderId == purchaseOrderId).OrderBy(l => l.LineNumber).ToListAsync(ct);
        var lines = new List<ReceivableLineDto>();
        foreach (var l in poLines)
        {
            var already = await AlreadyReceivedAsync(l.Id, excludeGoodsReceiptId: null, ct);
            lines.Add(new ReceivableLineDto(l.Id, l.ItemDescription, l.Uom, l.Quantity, already, Math.Max(0, l.Quantity - already)));
        }

        var status = lines.Count > 0 && lines.All(l => l.QuantityRemaining <= 0) ? "FullyReceived"
            : lines.Any(l => l.QuantityAlreadyReceived > 0) ? "PartiallyReceived"
            : "NotReceived";

        return new PurchaseOrderReceiptStatusDto(status, lines);
    }

    // --- helpers ---------------------------------------------------------------------

    /// <summary>Builds and validates the line set for Create/Update/Amend - shared so the "don't over-receive" rule lives in exactly one place.</summary>
    private async Task<List<GoodsReceiptLine>> BuildLinesAsync(Guid purchaseOrderId, IReadOnlyList<CreateGoodsReceiptLineRequest> requestLines, Guid? excludeGoodsReceiptId, CancellationToken ct)
    {
        if (requestLines.Count == 0)
        {
            throw new InvalidOperationException("A goods receipt needs at least one line.");
        }

        var poLines = await _db.PurchaseOrderLines.Where(l => l.PurchaseOrderId == purchaseOrderId).ToDictionaryAsync(l => l.Id, ct);
        var result = new List<GoodsReceiptLine>();

        foreach (var rl in requestLines)
        {
            if (rl.QuantityReceived <= 0)
            {
                throw new InvalidOperationException("Quantity received must be greater than zero for every line.");
            }
            if (rl.QuantityRejected < 0 || rl.QuantityRejected > rl.QuantityReceived)
            {
                throw new InvalidOperationException("Quantity rejected cannot be negative or exceed quantity received.");
            }
            if (!poLines.TryGetValue(rl.PurchaseOrderLineId, out var poLine))
            {
                throw new InvalidOperationException("One of the lines does not belong to the source purchase order.");
            }

            var alreadyReceived = await AlreadyReceivedAsync(rl.PurchaseOrderLineId, excludeGoodsReceiptId, ct);
            var remaining = poLine.Quantity - alreadyReceived;
            if (rl.QuantityReceived > remaining)
            {
                throw new InvalidOperationException($"Cannot receive {rl.QuantityReceived} {poLine.Uom} of '{poLine.ItemDescription}' - only {remaining} remains on the PO.");
            }

            result.Add(new GoodsReceiptLine
            {
                PurchaseOrderLineId = poLine.Id,
                ItemDescription = poLine.ItemDescription,
                Uom = poLine.Uom,
                QuantityOrdered = poLine.Quantity,
                QuantityReceived = rl.QuantityReceived,
                QuantityAccepted = rl.QuantityReceived - rl.QuantityRejected,
                QuantityRejected = rl.QuantityRejected,
                InspectionStatus = DeriveInspectionStatus(rl.QuantityReceived, rl.QuantityRejected)
            });
        }

        return result;
    }

    /// <summary>Sum of QuantityReceived across every *Posted* GR line for this PO line - Draft/Cancelled GRs never count. Excludes one GR (used when amending it, so its own quantities aren't double-counted against themselves).</summary>
    private async Task<decimal> AlreadyReceivedAsync(Guid purchaseOrderLineId, Guid? excludeGoodsReceiptId, CancellationToken ct)
    {
        var query =
            from line in _db.GoodsReceiptLines
            join receipt in _db.GoodsReceipts on line.GoodsReceiptId equals receipt.Id
            where line.PurchaseOrderLineId == purchaseOrderLineId
                && receipt.Status == GoodsReceiptStatus.Posted
                && (excludeGoodsReceiptId == null || receipt.Id != excludeGoodsReceiptId)
            select line.QuantityReceived;
        return await query.SumAsync(ct);
    }

    private static InspectionStatus DeriveInspectionStatus(decimal received, decimal rejected)
    {
        if (rejected <= 0) return InspectionStatus.Accepted;
        if (rejected >= received) return InspectionStatus.Rejected;
        return InspectionStatus.PartiallyAccepted;
    }

    private async Task<string> NextNumberAsync(CancellationToken ct)
    {
        var count = await _db.Documents.CountAsync(d => d.DocumentType == EntityType, ct);
        return $"GR-{count + 1:D5}";
    }

    private async Task<string> UserNameAsync(Guid userId, CancellationToken ct)
        => await _db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? userId.ToString();

    private static string Snapshot(GoodsReceipt gr) => JsonSerializer.Serialize(new GrSnapshot(
        gr.DeliveryDate, gr.DeliveryNoteNumber, gr.Location,
        gr.Lines.Select(l => new GrSnapshotLine(l.ItemDescription, l.Uom, l.QuantityOrdered, l.QuantityReceived, l.QuantityAccepted, l.QuantityRejected, l.InspectionStatus.ToString())).ToList(),
        gr.CustomFieldsJson));

    // See PurchaseOrderService.DeserializeCustomFields's comment on the empty-string guard.
    private static IReadOnlyDictionary<string, string> DeserializeCustomFields(string json) =>
        string.IsNullOrWhiteSpace(json) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

    private sealed record GrSnapshot(DateOnly DeliveryDate, string? DeliveryNoteNumber, string? Location, List<GrSnapshotLine> Lines, string CustomFieldsJson);
    private sealed record GrSnapshotLine(string ItemDescription, string Uom, decimal QuantityOrdered, decimal QuantityReceived, decimal QuantityAccepted, decimal QuantityRejected, string InspectionStatus);
}
