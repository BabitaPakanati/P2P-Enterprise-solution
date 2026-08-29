using Microsoft.EntityFrameworkCore;
using P2P.Application.Abstractions;
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

    public string EntityType => "PurchaseRequisition";

    public PurchaseRequisitionService(AppDbContext db, IWorkflowEngine workflowEngine, ICurrentUserContext currentUser)
    {
        _db = db;
        _workflowEngine = workflowEngine;
        _currentUser = currentUser;
    }

    public async Task<Guid> CreateAsync(Guid requesterId, CreateRequisitionRequest request, CancellationToken ct = default)
    {
        if (request.Lines.Count == 0)
        {
            throw new InvalidOperationException("A requisition needs at least one line.");
        }
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0) throw new InvalidOperationException($"Line '{line.ItemDescription}': quantity must be greater than zero.");
            if (string.IsNullOrWhiteSpace(line.Uom)) throw new InvalidOperationException($"Line '{line.ItemDescription}': UOM is required.");
        }
        if (request.RequiredByDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new InvalidOperationException("Required-by date cannot be in the past.");
        }

        var number = await NextNumberAsync("PR", ct);
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

        var lineNo = 1;
        foreach (var l in request.Lines)
        {
            pr.AddLine(new PurchaseRequisitionLine
            {
                PurchaseRequisitionId = pr.Id,
                LineNumber = lineNo++,
                ItemDescription = l.ItemDescription,
                Quantity = l.Quantity,
                Uom = l.Uom,
                EstimatedUnitPrice = l.EstimatedUnitPrice
            });
        }
        pr.EstimatedValue = pr.Lines.Sum(l => l.EstimatedValue);
        version.PayloadJson = SnapshotJson(pr);

        _db.Documents.Add(document);
        _db.DocumentVersions.Add(version);
        _db.PurchaseRequisitions.Add(pr);
        _db.AuditLogs.Add(AuditLog.Create(EntityType, pr.Id, "CREATE", requesterId, await UserNameAsync(requesterId, ct), version.Id, source: "API"));

        await _db.SaveChangesAsync(ct);
        return pr.Id;
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
            pr.Lines.Select(l => new RequisitionLineDto(l.Id, l.LineNumber, l.ItemDescription, l.Quantity, l.Uom, l.EstimatedUnitPrice, l.EstimatedValue)).ToList());
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

    // --- IWorkflowCompletionHandler ------------------------------------------------

    public async Task OnApprovedAsync(Guid entityId, Guid documentVersionId, CancellationToken ct = default)
    {
        var pr = await _db.PurchaseRequisitions.FindAsync([entityId], ct);
        var version = await _db.DocumentVersions.FindAsync([documentVersionId], ct);
        if (pr is null || version is null) return;

        var document = await _db.Documents.FindAsync([pr.DocumentId], ct);
        pr.Status = PurchaseRequisitionStatus.Approved;
        version.VersionStatus = DocumentVersionStatus.Active;
        if (document is not null) document.CurrentStatus = nameof(PurchaseRequisitionStatus.Approved);

        await _db.SaveChangesAsync(ct);
    }

    public async Task OnRejectedAsync(Guid entityId, Guid documentVersionId, string? reason, CancellationToken ct = default)
    {
        var pr = await _db.PurchaseRequisitions.FindAsync([entityId], ct);
        var version = await _db.DocumentVersions.FindAsync([documentVersionId], ct);
        if (pr is null || version is null) return;

        var document = await _db.Documents.FindAsync([pr.DocumentId], ct);
        pr.Status = PurchaseRequisitionStatus.Rejected;
        version.VersionStatus = DocumentVersionStatus.Rejected;
        version.ChangeReason ??= reason;
        if (document is not null) document.CurrentStatus = nameof(PurchaseRequisitionStatus.Rejected);

        await _db.SaveChangesAsync(ct);
    }

    // --- helpers ---------------------------------------------------------------------

    private async Task<string> NextNumberAsync(string prefix, CancellationToken ct)
    {
        var count = await _db.Documents.CountAsync(d => d.DocumentType == EntityType, ct);
        return $"{prefix}-{count + 1:D5}";
    }

    private async Task<string> UserNameAsync(Guid userId, CancellationToken ct)
        => await _db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? userId.ToString();

    private static string SnapshotJson(PurchaseRequisition pr) => System.Text.Json.JsonSerializer.Serialize(new
    {
        pr.RequisitionNumber,
        pr.Description,
        pr.Category,
        pr.EstimatedValue,
        pr.Currency,
        Lines = pr.Lines.Select(l => new { l.ItemDescription, l.Quantity, l.Uom, l.EstimatedUnitPrice })
    });
}
