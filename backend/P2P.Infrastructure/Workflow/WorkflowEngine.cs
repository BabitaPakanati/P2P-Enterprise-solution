using Microsoft.EntityFrameworkCore;
using P2P.Application.Abstractions;
using P2P.Application.Workflow;
using P2P.Domain.Identity;
using P2P.Domain.Workflow;
using P2P.Infrastructure.Persistence;

namespace P2P.Infrastructure.Workflow;

/// <summary>
/// Reference implementation of the generic engine described in §21 of the
/// requirements. Deliberately scoped for the vertical slice: it resolves exactly one
/// matching step (the first whose rules pass) and opens exactly one approval task -
/// sequential multi-step chains, parallel approval, delegation and escalation are
/// Phase 4 work (see docs/ARCHITECTURE.md's roadmap) and would extend
/// <see cref="StartAsync"/> and <see cref="ApprovalService"/> without changing the
/// shape callers depend on.
/// </summary>
public sealed class WorkflowEngine : IWorkflowEngine
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public WorkflowEngine(AppDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> StartAsync(
        string entityType, Guid entityId, Guid documentVersionId,
        IReadOnlyDictionary<string, decimal> context, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var definition = await _db.WorkflowDefinitions
            .FirstOrDefaultAsync(d => d.EntityType == entityType && d.Status == WorkflowDefinitionStatus.Active, ct)
            ?? throw new InvalidOperationException(
                $"No active workflow is configured for entity type '{entityType}'. " +
                "Run POST /api/v1/_diagnostics/seed-foundation for this organisation first.");

        var version = await _db.WorkflowVersions
            .Where(v => v.WorkflowDefinitionId == definition.Id
                        && v.Status == WorkflowVersionStatus.Active
                        && v.EffectiveFrom <= today
                        && (v.EffectiveTo == null || v.EffectiveTo >= today))
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                $"Workflow '{definition.Name}' has no version effective today ({today}).");

        var steps = await _db.WorkflowSteps
            .Where(s => s.WorkflowVersionId == version.Id)
            .OrderBy(s => s.Sequence)
            .ToListAsync(ct);

        var instance = new WorkflowInstance
        {
            WorkflowVersionId = version.Id,
            EntityType = entityType,
            EntityId = entityId,
            Status = WorkflowInstanceStatus.Running,
            CreatedBy = _currentUser.UserId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        _db.WorkflowInstances.Add(instance);

        foreach (var step in steps)
        {
            var rules = await _db.WorkflowRules.Where(r => r.WorkflowStepId == step.Id).ToListAsync(ct);
            if (rules.Count > 0 && !rules.All(r => Evaluate(r, context)))
            {
                continue; // this step's conditions don't apply to this transaction - try the next
            }

            var assignment = await _db.AuthorityAssignments
                .Where(a => a.RoleId == step.ApprovalRoleId
                            && a.Status == AuthorityAssignmentStatus.Active
                            && a.EffectiveFrom <= today
                            && (a.EffectiveTo == null || a.EffectiveTo >= today)
                            && (a.AmountFrom == null || !context.ContainsKey("Amount") || context["Amount"] >= a.AmountFrom)
                            && (a.AmountTo == null || !context.ContainsKey("Amount") || context["Amount"] <= a.AmountTo))
                .OrderBy(a => a.Priority)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException(
                    $"No effective authority assignment covers step '{step.StepName}' for this amount.");

            _db.ApprovalTasks.Add(new ApprovalTask
            {
                WorkflowInstanceId = instance.Id,
                WorkflowStepId = step.Id,
                AssignedToUserId = assignment.UserId,
                Status = ApprovalTaskStatus.Pending,
                CreatedBy = _currentUser.UserId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            await _db.SaveChangesAsync(ct);
            return instance.Id;
        }

        throw new InvalidOperationException(
            $"No step in workflow '{definition.Name}' v{version.VersionNumber} matched this transaction's conditions.");
    }

    private static bool Evaluate(WorkflowRule rule, IReadOnlyDictionary<string, decimal> context)
    {
        if (!context.TryGetValue(rule.Attribute, out var actual) || !decimal.TryParse(rule.Value, out var expected))
        {
            return false;
        }

        return rule.Operator switch
        {
            RuleOperator.Equals => actual == expected,
            RuleOperator.NotEquals => actual != expected,
            RuleOperator.GreaterThan => actual > expected,
            RuleOperator.LessThan => actual < expected,
            RuleOperator.GreaterOrEqual => actual >= expected,
            RuleOperator.LessOrEqual => actual <= expected,
            _ => false // In/NotIn are set-membership operators, not meaningful against a single numeric attribute here
        };
    }
}
