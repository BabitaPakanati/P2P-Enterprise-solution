namespace P2P.Application.Workflow;

/// <summary>
/// How a completed workflow instance gets back to the entity that started it,
/// without the generic engine needing to know PurchaseRequisition or PurchaseOrder
/// exist. Each module (Procurement, and every module that follows it) registers one
/// handler per entity type; IApprovalService dispatches to whichever handler's
/// <see cref="EntityType"/> matches the instance that just finished.
/// </summary>
public interface IWorkflowCompletionHandler
{
    string EntityType { get; }

    Task OnApprovedAsync(Guid entityId, Guid documentVersionId, CancellationToken ct = default);

    Task OnRejectedAsync(Guid entityId, Guid documentVersionId, string? reason, CancellationToken ct = default);
}
