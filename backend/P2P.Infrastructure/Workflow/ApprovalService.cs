using Microsoft.EntityFrameworkCore;
using P2P.Application.Workflow;
using P2P.Domain.Audit;
using P2P.Domain.Identity;
using P2P.Domain.Procurement;
using P2P.Domain.Versioning;
using P2P.Domain.Workflow;
using P2P.Infrastructure.Persistence;

namespace P2P.Infrastructure.Workflow;

public sealed class ApprovalService : IApprovalService
{
    private readonly AppDbContext _db;
    private readonly IEnumerable<IWorkflowCompletionHandler> _completionHandlers;

    public ApprovalService(AppDbContext db, IEnumerable<IWorkflowCompletionHandler> completionHandlers)
    {
        _db = db;
        _completionHandlers = completionHandlers;
    }

    public async Task<IReadOnlyList<ApprovalTaskDto>> GetMyPendingTasksAsync(Guid userId, CancellationToken ct = default)
    {
        var tasks = await (
            from task in _db.ApprovalTasks
            join instance in _db.WorkflowInstances on task.WorkflowInstanceId equals instance.Id
            where task.AssignedToUserId == userId && task.Status == ApprovalTaskStatus.Pending
            select new { task.Id, InstanceId = instance.Id, instance.EntityType, instance.EntityId, task.CreatedAtUtc }
        ).ToListAsync(ct);

        var result = new List<ApprovalTaskDto>(tasks.Count);
        foreach (var t in tasks)
        {
            // Two entity types exist today; a registry-of-providers replaces this switch
            // as more modules plug into the same generic engine.
            var (number, requesterId, amount, currency) = t.EntityType switch
            {
                "PurchaseRequisition" => await SummariseRequisitionAsync(t.EntityId, ct),
                "PurchaseOrder" => await SummariseOrderAsync(t.EntityId, ct),
                _ => (t.EntityId.ToString(), Guid.Empty, 0m, "USD")
            };
            var requesterName = await _db.Users.Where(u => u.Id == requesterId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? "Unknown";

            result.Add(new ApprovalTaskDto(t.Id, t.InstanceId, t.EntityType, t.EntityId, number, requesterName, amount, currency, "Pending", t.CreatedAtUtc));
        }
        return result;
    }

    public async Task DecideAsync(Guid taskId, Guid decidingUserId, bool approve, string? comments, CancellationToken ct = default)
    {
        var task = await _db.ApprovalTasks.FindAsync([taskId], ct)
            ?? throw new InvalidOperationException("Approval task not found.");
        if (task.Status != ApprovalTaskStatus.Pending)
        {
            throw new InvalidOperationException($"Task is already '{task.Status}'.");
        }
        if (task.AssignedToUserId != decidingUserId)
        {
            throw new InvalidOperationException("Only the assigned approver may decide this task.");
        }

        var instance = await _db.WorkflowInstances.FindAsync([task.WorkflowInstanceId], ct)
            ?? throw new InvalidOperationException("Workflow instance not found.");

        // Maker-checker (§2.5): the person who created the underlying transaction may
        // never be the one who approves it, even if somehow also the assigned approver.
        var requesterId = instance.EntityType switch
        {
            "PurchaseRequisition" => await _db.PurchaseRequisitions.Where(p => p.Id == instance.EntityId).Select(p => (Guid?)p.RequesterId).FirstOrDefaultAsync(ct),
            "PurchaseOrder" => await _db.PurchaseOrders.Where(p => p.Id == instance.EntityId).Select(p => (Guid?)p.BuyerId).FirstOrDefaultAsync(ct),
            _ => null
        };
        if (requesterId == decidingUserId)
        {
            throw new InvalidOperationException("Maker-checker violation: the requester cannot approve their own transaction.");
        }

        task.Status = approve ? ApprovalTaskStatus.Approved : ApprovalTaskStatus.Rejected;
        task.DecidedAtUtc = DateTimeOffset.UtcNow;
        task.Comments = comments;
        task.UpdatedBy = decidingUserId;
        task.UpdatedAtUtc = DateTimeOffset.UtcNow;

        instance.Status = approve ? WorkflowInstanceStatus.Approved : WorkflowInstanceStatus.Rejected;
        instance.UpdatedBy = decidingUserId;
        instance.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var documentVersionId = await _db.DocumentVersions
            .Where(v => v.WorkflowInstanceId == instance.Id)
            .Select(v => v.Id)
            .FirstOrDefaultAsync(ct);

        _db.AuditLogs.Add(AuditLog.Create(
            entityType: instance.EntityType, entityId: instance.EntityId, action: approve ? "APPROVE" : "REJECT",
            userId: decidingUserId, userName: await _db.Users.Where(u => u.Id == decidingUserId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? decidingUserId.ToString(),
            entityVersionId: documentVersionId, source: "API", reason: comments));

        await _db.SaveChangesAsync(ct);

        var handler = _completionHandlers.FirstOrDefault(h => h.EntityType == instance.EntityType);
        if (handler is not null)
        {
            if (approve)
            {
                await handler.OnApprovedAsync(instance.EntityId, documentVersionId, ct);
            }
            else
            {
                await handler.OnRejectedAsync(instance.EntityId, documentVersionId, comments, ct);
            }
        }
    }

    private async Task<(string Number, Guid RequesterId, decimal Amount, string Currency)> SummariseRequisitionAsync(Guid id, CancellationToken ct)
    {
        var pr = await _db.PurchaseRequisitions.FindAsync([id], ct);
        return pr is null ? (id.ToString(), Guid.Empty, 0m, "USD") : (pr.RequisitionNumber, pr.RequesterId, pr.EstimatedValue, pr.Currency);
    }

    private async Task<(string Number, Guid RequesterId, decimal Amount, string Currency)> SummariseOrderAsync(Guid id, CancellationToken ct)
    {
        var po = await _db.PurchaseOrders.FindAsync([id], ct);
        return po is null ? (id.ToString(), Guid.Empty, 0m, "USD") : (po.PoNumber, po.BuyerId, po.TotalValue, po.Currency);
    }
}
