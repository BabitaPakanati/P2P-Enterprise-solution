namespace P2P.Application.Workflow;

/// <summary>
/// Evaluates the currently-effective workflow version for an entity type against a
/// business context, and opens the first approval task(s). Deliberately generic -
/// PurchaseRequisition and PurchaseOrder call the exact same engine, driven only by
/// data (WorkflowDefinition/Version/Step/Rule), never by entity-specific code. This
/// is the mechanism the requirements document insists on in §21 ("must not be
/// specific to Invoice or PO").
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Starts a workflow instance for one document version. <paramref name="context"/>
    /// carries the attributes rules are evaluated against (at minimum "Amount"); the
    /// engine resolves the effective WorkflowVersion for <paramref name="entityType"/>
    /// as of today, finds the first step whose rules are satisfied, and creates an
    /// ApprovalTask assigned to whoever currently holds that step's approval role for
    /// this amount (via AuthorityAssignment). Returns the new WorkflowInstance's id.
    /// </summary>
    Task<Guid> StartAsync(
        string entityType,
        Guid entityId,
        Guid documentVersionId,
        IReadOnlyDictionary<string, decimal> context,
        CancellationToken ct = default);
}
