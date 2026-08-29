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

/// <summary>
/// Implements both the requisition use cases and the workflow-completion callback
/// for entity type "PurchaseRequisition" - see IWorkflowCompletionHandler. Registered
/// once in DI, exposed as both interfaces (Program.cs), same pattern as TenantContext.
/// </summary>
public sealed class PurchaseRequisitionService : IPurchaseRequisitionService, IWorkflowCompletionHandler
{
    private readonly AppDbContext _db;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ICurrentUserContext _currentUser;
    private readonly ICustomFieldValidator _customFields;

    public string EntityType => "PurchaseRequisition";

    public PurchaseRequisitionService(AppDbContext db, IWorkflowEngine workflowEngine, ICurrentUserContext currentUser, ICustomFieldValidator customFields)
    {
        _db = db;
        _workflowEngine = workflowEngine;
        _currentUser = currentUser;
        _customFields = customFields;
    }

    public async Task<Guid> CreateAsync(Guid requesterId, CreateRequisitionRequest request, CancellationToken ct = default)
    {
        ValidateLines(request.Lines);
        ValidateRequiredByDate(request.RequiredByDate);

        var number = await NextNumberAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var document = new Document
        {
            DocumentNumber = number,
            DocumentType = EntityType,
            CurrentStatus = nameof(PurchaseRequisitionStatus.Draft),
            CreatedBy = requesterId,
            CreatedAtUtc = now
        };
        var version = new DocumentVersion
        {
            DocumentId = document.Id,
            VersionNumber = 1,
            VersionStatus = DocumentVersionStatus.Draft,
            EffectiveFrom = now,
            CreatedBy = requesterId,
            CreatedAtUtc = now
        };
        document.CurrentVersionId = version.Id;

        var pr = new PurchaseRequisition
        {
            DocumentId = document.Id,
            RequisitionNumber = number,
            RequesterId = requesterId,
            RequestDate = DateOnly.FromDateTime(now.UtcDateTime),
            RequiredByDate = request.RequiredByDate,
            RequisitionType = request.RequisitionType,
            Description = request.Description,
            Category = request.Category,
            PreferredSupplierName = request.PreferredSupplierName,
            Currency = request.Currency,
            Status = PurchaseRequisitionStatus.Draft,
            CreatedBy = requesterId,
            CreatedAtUtc = now
        };
        foreach (var line in ToLines(request.Lines, pr.Id))
        {
            pr.AddLine(line);
        }
        pr.EstimatedValue = pr.Lines.Sum(l => l.EstimatedValue);
        pr.CustomFieldsJson = await _customFields.ValidateAndSerializeAsync(EntityType, request.CustomFields, ct);
        version.PayloadJson = Snapshot(pr);

        _db.Documents.Add(document);
        _db.DocumentVersions.Add(version);
        _db.PurchaseRequisitions.Add(pr);
        _db.AuditLogs.Add(AuditLog.Create(EntityType, pr.Id, "CREATE", requesterId, await UserNameAsync(requesterId, ct), version.Id, source: "API"));

        await _db.SaveChangesAsync(ct);
        return pr.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateRequisitionRequest request, CancellationToken ct = default)
    {
        var pr = await _db.PurchaseRequisitions.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new InvalidOperationException("Requisition not found.");
        if (pr.Status != PurchaseRequisitionStatus.Draft)
        {
            throw new InvalidOperationException($"Only a Draft requisition can be edited (current status: {pr.Status}). Once approved, use Amend instead.");
        }
        ValidateLines(request.Lines);
        ValidateRequiredByDate(request.RequiredByDate);

        pr.RequiredByDate = request.RequiredByDate;
        pr.RequisitionType = request.RequisitionType;
        pr.Description = request.Description;
        pr.Category = request.Category;
        pr.Currency = request.Currency;
        pr.PreferredSupplierName = request.PreferredSupplierName;

        var newLines = ToLines(request.Lines, pr.Id).ToList();
        _db.PurchaseRequisitionLines.RemoveRange(pr.Lines);
        _db.PurchaseRequisitionLines.AddRange(newLines);
        pr.ReplaceLines(newLines);
        pr.EstimatedValue = pr.Lines.Sum(l => l.EstimatedValue);
        pr.CustomFieldsJson = await _customFields.ValidateAndSerializeAsync(EntityType, request.CustomFields, ct);
        pr.UpdatedBy = _currentUser.UserId;
        pr.UpdatedAtUtc = DateTimeOffset.UtcNow;

        // Still Draft, never submitted - refresh the v1 snapshot in place rather than
        // minting a new version. Nothing has been committed yet for history to protect.
        var document = await _db.Documents.FindAsync([pr.DocumentId], ct);
        if (document?.CurrentVersionId is Guid versionId)
        {
            var version = await _db.DocumentVersions.FindAsync([versionId], ct);
            if (version is not null) version.PayloadJson = Snapshot(pr);
        }

        _db.AuditLogs.Add(AuditLog.Create(EntityType, pr.Id, "UPDATE", _currentUser.UserId, await UserNameAsync(_currentUser.UserId, ct), document?.CurrentVersionId, source: "API"));
        await _db.SaveChangesAsync(ct);
    }

    public async Task SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var pr = await _db.PurchaseRequisitions.FindAsync([id], ct) ?? throw new InvalidOperationException("Requisition not found.");
        if (pr.Status != PurchaseRequisitionStatus.Draft)
        {
            throw new InvalidOperationException($"Only a Draft requisition can be submitted (current status: {pr.Status}).");
        }

        var document = await _db.Documents.FindAsync([pr.DocumentId], ct)!;
        var version = await _db.DocumentVersions.FindAsync([document!.CurrentVersionId!.Value], ct)!;

        // One transaction spanning both SaveChanges calls below AND the ones inside
        // WorkflowEngine.StartAsync (same DbContext instance, same DB connection) -
        // if the engine can't resolve an approver (e.g. a misconfigured workflow
        // with no matching AuthorityAssignment), everything rolls back and the
        // requisition stays Draft, instead of being left PendingApproval with no
        // approval task anyone can ever act on. Found live, not hypothetically -
        // see docs/ARCHITECTURE.md.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        pr.Status = PurchaseRequisitionStatus.PendingApproval;
        pr.UpdatedBy = _currentUser.UserId;
        pr.UpdatedAtUtc = DateTimeOffset.UtcNow;
        document.CurrentStatus = nameof(PurchaseRequisitionStatus.PendingApproval);
        version!.VersionStatus = DocumentVersionStatus.PendingApproval;

        _db.AuditLogs.Add(AuditLog.Create(EntityType, pr.Id, "SUBMIT", _currentUser.UserId, await UserNameAsync(_currentUser.UserId, ct), version.Id, source: "API"));
        await _db.SaveChangesAsync(ct);

        var instanceId = await _workflowEngine.StartAsync(
            EntityType, pr.Id, version.Id,
            new Dictionary<string, decimal> { ["Amount"] = pr.EstimatedValue }, ct);

        version.WorkflowInstanceId = instanceId;
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task CancelAsync(Guid id, CancellationToken ct = default)
    {
        var pr = await _db.PurchaseRequisitions.FindAsync([id], ct) ?? throw new InvalidOperationException("Requisition not found.");
        if (pr.Status is not (PurchaseRequisitionStatus.Draft or PurchaseRequisitionStatus.PendingApproval))
        {
            throw new InvalidOperationException($"A requisition in status '{pr.Status}' cannot be cancelled.");
        }

        var document = await _db.Documents.FindAsync([pr.DocumentId], ct);
        var version = document?.CurrentVersionId is Guid vId ? await _db.DocumentVersions.FindAsync([vId], ct) : null;

        pr.Status = PurchaseRequisitionStatus.Cancelled;
        pr.UpdatedBy = _currentUser.UserId;
        pr.UpdatedAtUtc = DateTimeOffset.UtcNow;
        if (document is not null)
        {
            document.CurrentStatus = nameof(PurchaseRequisitionStatus.Cancelled);
            document.ClosedAtUtc = DateTimeOffset.UtcNow;
        }
        if (version is not null)
        {
            version.VersionStatus = DocumentVersionStatus.Cancelled;

            var openTasks = await _db.ApprovalTasks
                .Where(t => t.Status == ApprovalTaskStatus.Pending)
                .Join(_db.WorkflowInstances.Where(i => i.EntityType == EntityType && i.EntityId == pr.Id),
                      t => t.WorkflowInstanceId, i => i.Id, (t, i) => t)
                .ToListAsync(ct);
            foreach (var t in openTasks)
            {
                t.Status = ApprovalTaskStatus.Rejected;
                t.Comments = "Cancelled by requester before a decision was made.";
                t.DecidedAtUtc = DateTimeOffset.UtcNow;
            }
        }

        _db.AuditLogs.Add(AuditLog.Create(EntityType, pr.Id, "CANCEL", _currentUser.UserId, await UserNameAsync(_currentUser.UserId, ct), version?.Id, source: "API"));
        await _db.SaveChangesAsync(ct);
    }

    public async Task AmendAsync(Guid id, Guid amendedBy, AmendRequisitionRequest request, CancellationToken ct = default)
    {
        var pr = await _db.PurchaseRequisitions.FindAsync([id], ct) ?? throw new InvalidOperationException("Requisition not found.");
        if (pr.Status != PurchaseRequisitionStatus.Approved)
        {
            throw new InvalidOperationException(
                $"A requisition in status '{pr.Status}' cannot be amended - it must be Approved and not yet converted to a purchase order.");
        }
        ValidateLines(request.Lines);
        ValidateRequiredByDate(request.RequiredByDate);
        if (string.IsNullOrWhiteSpace(request.ChangeReason))
        {
            throw new InvalidOperationException("A change reason is required to amend a requisition.");
        }

        var document = (await _db.Documents.FindAsync([pr.DocumentId], ct))!;
        var currentVersion = (await _db.DocumentVersions.FindAsync([document.CurrentVersionId!.Value], ct))!;
        var now = DateTimeOffset.UtcNow;

        var proposedTotal = request.Lines.Sum(l => l.Quantity * l.EstimatedUnitPrice);
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
            PayloadJson = JsonSerializer.Serialize(new PrSnapshot(
                request.Description, request.Category, request.RequisitionType, request.RequiredByDate,
                request.PreferredSupplierName, proposedTotal, request.Currency,
                request.Lines.Select(l => new PrSnapshotLine(l.ItemDescription, l.Quantity, l.Uom, l.EstimatedUnitPrice)).ToList(),
                proposedCustomFields))
        };

        // pr's live fields and currentVersion are untouched - the proposal lives only
        // in newVersion until approved, exactly like PurchaseOrderService.AmendAsync.
        document.CurrentStatus = nameof(DocumentVersionStatus.PendingApproval);

        // See SubmitAsync's comment on why this whole thing is one transaction.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        _db.DocumentVersions.Add(newVersion);
        _db.AuditLogs.Add(AuditLog.Create(EntityType, pr.Id, "SUBMIT", amendedBy, await UserNameAsync(amendedBy, ct), newVersion.Id, source: "API", reason: request.ChangeReason,
            comments: $"Amendment: v{currentVersion.VersionNumber} -> v{newVersion.VersionNumber}"));
        await _db.SaveChangesAsync(ct);

        var instanceId = await _workflowEngine.StartAsync(EntityType, pr.Id, newVersion.Id, new Dictionary<string, decimal> { ["Amount"] = proposedTotal }, ct);
        newVersion.WorkflowInstanceId = instanceId;
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<RequisitionDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var pr = await _db.PurchaseRequisitions.Include(p => p.Lines).AsSplitQuery().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pr is null) return null;

        var document = await _db.Documents.FindAsync([pr.DocumentId], ct);
        var versionNumber = document?.CurrentVersionId is Guid vId
            ? await _db.DocumentVersions.Where(v => v.Id == vId).Select(v => v.VersionNumber).FirstOrDefaultAsync(ct)
            : 1;

        return new RequisitionDetailDto(
            pr.Id, pr.DocumentId, pr.RequisitionNumber, pr.RequesterId, pr.RequestDate, pr.RequiredByDate,
            pr.RequisitionType, pr.Description, pr.Category, pr.PreferredSupplierName, pr.EstimatedValue, pr.Currency,
            pr.Status.ToString(), versionNumber,
            pr.Lines.Select(l => new RequisitionLineDto(l.Id, l.LineNumber, l.ItemDescription, l.Quantity, l.Uom, l.EstimatedUnitPrice, l.EstimatedValue)).ToList(),
            DeserializeCustomFields(pr.CustomFieldsJson));
    }

    public async Task<IReadOnlyList<RequisitionSummaryDto>> ListAsync(Guid? requesterId, CancellationToken ct = default)
    {
        var query = _db.PurchaseRequisitions.AsQueryable();
        if (requesterId is Guid rid) query = query.Where(p => p.RequesterId == rid);

        return await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new RequisitionSummaryDto(p.Id, p.RequisitionNumber, p.RequesterId, p.RequestDate, p.RequiredByDate, p.Category, p.Description, p.EstimatedValue, p.Currency, p.Status.ToString()))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentVersionDto>> GetVersionHistoryAsync(Guid id, CancellationToken ct = default)
    {
        var pr = await _db.PurchaseRequisitions.FindAsync([id], ct) ?? throw new InvalidOperationException("Requisition not found.");
        return await _db.DocumentVersions
            .Where(v => v.DocumentId == pr.DocumentId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new DocumentVersionDto(v.Id, v.VersionNumber, v.VersionStatus.ToString(), v.EffectiveFrom, v.EffectiveTo, v.ChangeReason, v.ChangeComment, v.PayloadJson))
            .ToListAsync(ct);
    }

    // --- IWorkflowCompletionHandler ------------------------------------------------

    public async Task OnApprovedAsync(Guid entityId, Guid documentVersionId, CancellationToken ct = default)
    {
        var pr = await _db.PurchaseRequisitions.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == entityId, ct);
        var version = await _db.DocumentVersions.FindAsync([documentVersionId], ct);
        if (pr is null || version is null) return;
        var document = await _db.Documents.FindAsync([pr.DocumentId], ct);

        if (version.VersionNumber == 1)
        {
            pr.Status = PurchaseRequisitionStatus.Approved;
        }
        else
        {
            // An amendment: apply the proposed snapshot now that it's approved, and
            // only now supersede the version that was effective until this moment -
            // same pattern as PurchaseOrderService.OnApprovedAsync.
            var snapshot = JsonSerializer.Deserialize<PrSnapshot>(version.PayloadJson)!;
            pr.Description = snapshot.Description;
            pr.Category = snapshot.Category;
            pr.RequisitionType = snapshot.RequisitionType;
            pr.RequiredByDate = snapshot.RequiredByDate;
            pr.PreferredSupplierName = snapshot.PreferredSupplierName;
            pr.EstimatedValue = snapshot.EstimatedValue;
            pr.CustomFieldsJson = snapshot.CustomFieldsJson;

            var newLines = snapshot.Lines.Select((l, i) => new PurchaseRequisitionLine
            {
                PurchaseRequisitionId = pr.Id, LineNumber = i + 1, ItemDescription = l.ItemDescription, Quantity = l.Quantity, Uom = l.Uom, EstimatedUnitPrice = l.EstimatedUnitPrice
            }).ToList();
            _db.PurchaseRequisitionLines.RemoveRange(pr.Lines);
            _db.PurchaseRequisitionLines.AddRange(newLines);
            pr.ReplaceLines(newLines);

            if (document?.CurrentVersionId is Guid previousVersionId)
            {
                var previous = await _db.DocumentVersions.FindAsync([previousVersionId], ct);
                if (previous is not null)
                {
                    previous.VersionStatus = DocumentVersionStatus.Superseded;
                    previous.EffectiveTo = DateTimeOffset.UtcNow;
                }
            }
            // pr.Status stays Approved - an amendment changes the fields, not the lifecycle stage.
        }

        pr.UpdatedBy = version.CreatedBy;
        pr.UpdatedAtUtc = DateTimeOffset.UtcNow;
        version.VersionStatus = DocumentVersionStatus.Active;
        if (document is not null)
        {
            document.CurrentVersionId = version.Id;
            document.CurrentStatus = pr.Status.ToString();
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task OnRejectedAsync(Guid entityId, Guid documentVersionId, string? reason, CancellationToken ct = default)
    {
        var pr = await _db.PurchaseRequisitions.FindAsync([entityId], ct);
        var version = await _db.DocumentVersions.FindAsync([documentVersionId], ct);
        if (pr is null || version is null) return;
        var document = await _db.Documents.FindAsync([pr.DocumentId], ct);

        version.VersionStatus = DocumentVersionStatus.Rejected;
        version.ChangeReason ??= reason;

        if (version.VersionNumber == 1)
        {
            pr.Status = PurchaseRequisitionStatus.Rejected;
        }
        // else: an amendment was rejected - the previously-Active version was never
        // touched and remains effective; pr's live fields are untouched too, by
        // construction (see AmendAsync's comment).

        if (document is not null) document.CurrentStatus = pr.Status.ToString();
        await _db.SaveChangesAsync(ct);
    }

    // --- helpers ---------------------------------------------------------------------

    private static void ValidateLines(IReadOnlyList<CreateRequisitionLineRequest> lines)
    {
        if (lines.Count == 0)
        {
            throw new InvalidOperationException("A requisition needs at least one line.");
        }
        foreach (var line in lines)
        {
            if (line.Quantity <= 0) throw new InvalidOperationException($"Line '{line.ItemDescription}': quantity must be greater than zero.");
            if (string.IsNullOrWhiteSpace(line.Uom)) throw new InvalidOperationException($"Line '{line.ItemDescription}': UOM is required.");
        }
    }

    private static void ValidateRequiredByDate(DateOnly requiredByDate)
    {
        if (requiredByDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new InvalidOperationException("Required-by date cannot be in the past.");
        }
    }

    private static IEnumerable<PurchaseRequisitionLine> ToLines(IReadOnlyList<CreateRequisitionLineRequest> lines, Guid purchaseRequisitionId) =>
        lines.Select((l, i) => new PurchaseRequisitionLine
        {
            PurchaseRequisitionId = purchaseRequisitionId,
            LineNumber = i + 1,
            ItemDescription = l.ItemDescription,
            Quantity = l.Quantity,
            Uom = l.Uom,
            EstimatedUnitPrice = l.EstimatedUnitPrice
        });

    private async Task<string> NextNumberAsync(CancellationToken ct)
    {
        var count = await _db.Documents.CountAsync(d => d.DocumentType == EntityType, ct);
        return $"PR-{count + 1:D5}";
    }

    private async Task<string> UserNameAsync(Guid userId, CancellationToken ct)
        => await _db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? userId.ToString();

    private static string Snapshot(PurchaseRequisition pr) => JsonSerializer.Serialize(new PrSnapshot(
        pr.Description, pr.Category, pr.RequisitionType, pr.RequiredByDate, pr.PreferredSupplierName, pr.EstimatedValue, pr.Currency,
        pr.Lines.Select(l => new PrSnapshotLine(l.ItemDescription, l.Quantity, l.Uom, l.EstimatedUnitPrice)).ToList(), pr.CustomFieldsJson));

    // See PurchaseOrderService.DeserializeCustomFields's comment on the empty-string guard.
    private static IReadOnlyDictionary<string, string> DeserializeCustomFields(string json) =>
        string.IsNullOrWhiteSpace(json) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

    private sealed record PrSnapshot(
        string Description, string Category, string RequisitionType, DateOnly RequiredByDate,
        string? PreferredSupplierName, decimal EstimatedValue, string Currency, List<PrSnapshotLine> Lines, string CustomFieldsJson);
    private sealed record PrSnapshotLine(string ItemDescription, decimal Quantity, string Uom, decimal EstimatedUnitPrice);
}
