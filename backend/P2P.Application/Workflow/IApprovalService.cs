namespace P2P.Application.Workflow;

public sealed record ApprovalTaskDto(
    Guid TaskId,
    Guid WorkflowInstanceId,
    string EntityType,
    Guid EntityId,
    string TransactionNumber,
    string Requester,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// The approver-facing half of the workflow engine: what's waiting on me, and what
/// happens when I decide. A decision here is what turns a document version from
/// PendingApproval into Active (or Rejected) - see PurchaseRequisitionService /
/// PurchaseOrderService, which subscribe to that transition rather than the other
/// way around, keeping the engine ignorant of which entity types exist.
/// </summary>
public interface IApprovalService
{
    Task<IReadOnlyList<ApprovalTaskDto>> GetMyPendingTasksAsync(Guid userId, CancellationToken ct = default);

    Task DecideAsync(Guid taskId, Guid decidingUserId, bool approve, string? comments, CancellationToken ct = default);
}
