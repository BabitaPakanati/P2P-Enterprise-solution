namespace P2P.Application.Admin;

public sealed record CreateWorkflowRuleRequest(string Attribute, string Operator, string Value, string Conjunction);
public sealed record CreateWorkflowStepRequest(string StepName, int Sequence, Guid ApprovalRoleId, bool IsMandatory, IReadOnlyList<CreateWorkflowRuleRequest> Rules);
public sealed record CreateWorkflowDefinitionRequest(string Name, string EntityType, string? Description, IReadOnlyList<CreateWorkflowStepRequest> Steps);
public sealed record CreateWorkflowVersionRequest(IReadOnlyList<CreateWorkflowStepRequest> Steps);

public sealed record WorkflowRuleDto(Guid Id, string Attribute, string Operator, string Value, string Conjunction);
public sealed record WorkflowStepDto(Guid Id, string StepName, int Sequence, Guid ApprovalRoleId, string ApprovalRoleName, bool IsMandatory, IReadOnlyList<WorkflowRuleDto> Rules);
public sealed record WorkflowVersionDto(Guid Id, int VersionNumber, string Status, DateOnly EffectiveFrom, DateOnly? EffectiveTo, IReadOnlyList<WorkflowStepDto> Steps);
public sealed record WorkflowDefinitionDto(Guid Id, string Name, string EntityType, string? Description, string Status, IReadOnlyList<WorkflowVersionDto> Versions);

/// <summary>
/// Entity types with a module actually wired to react to their approval outcome -
/// see IWorkflowCompletionHandler. A definition can technically be configured for
/// any string (the engine itself is fully generic), but only these currently do
/// anything useful once approved - the admin UI defaults to offering just these.
/// </summary>
public static class KnownWorkflowEntityTypes
{
    public const string PurchaseRequisition = "PurchaseRequisition";
    public const string PurchaseOrder = "PurchaseOrder";
    public static readonly IReadOnlyList<string> All = [PurchaseRequisition, PurchaseOrder];
}

/// <summary>
/// Admin surface over the workflow engine's own configuration tables
/// (WorkflowDefinition/Version/Step/Rule), which until now only FoundationSeeder
/// ever wrote to. Deliberately simplified relative to the full "draft, review,
/// publish" lifecycle §21/§74 of the requirements document imply: creating a
/// definition or a new version activates it immediately (EffectiveFrom = today).
/// What's not simplified away is the one thing that actually matters -
/// a version already used by a transaction is never edited or deleted; changing an
/// org's approval flow always means adding a new WorkflowVersion and retiring the
/// old one (EffectiveTo = today), exactly the mechanism WorkflowEngine already reads.
/// </summary>
public interface IWorkflowConfigService
{
    Task<IReadOnlyList<WorkflowDefinitionDto>> ListAsync(CancellationToken ct = default);
    Task<Guid> CreateDefinitionAsync(Guid createdBy, CreateWorkflowDefinitionRequest request, CancellationToken ct = default);
    Task<Guid> CreateNewVersionAsync(Guid definitionId, Guid createdBy, CreateWorkflowVersionRequest request, CancellationToken ct = default);
}

public sealed record CreateRoleRequest(string Code, string Name, string? Description);
public sealed record RoleDto(Guid Id, string Code, string Name, string? Description);

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateRoleRequest request, CancellationToken ct = default);
}
