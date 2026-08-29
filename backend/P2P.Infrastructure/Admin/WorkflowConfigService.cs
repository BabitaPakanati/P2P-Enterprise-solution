using Microsoft.EntityFrameworkCore;
using P2P.Application.Admin;
using P2P.Domain.Identity;
using P2P.Domain.Workflow;
using P2P.Infrastructure.Persistence;

namespace P2P.Infrastructure.Admin;

public sealed class WorkflowConfigService : IWorkflowConfigService
{
    private readonly AppDbContext _db;

    public WorkflowConfigService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<WorkflowDefinitionDto>> ListAsync(CancellationToken ct = default)
    {
        var definitions = await _db.WorkflowDefinitions.OrderBy(d => d.EntityType).ToListAsync(ct);
        var roles = await _db.Roles.ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var result = new List<WorkflowDefinitionDto>(definitions.Count);

        foreach (var def in definitions)
        {
            var versions = await _db.WorkflowVersions
                .Where(v => v.WorkflowDefinitionId == def.Id)
                .OrderByDescending(v => v.VersionNumber)
                .ToListAsync(ct);

            var versionDtos = new List<WorkflowVersionDto>(versions.Count);
            foreach (var v in versions)
            {
                var steps = await _db.WorkflowSteps.Where(s => s.WorkflowVersionId == v.Id).OrderBy(s => s.Sequence).ToListAsync(ct);
                var stepDtos = new List<WorkflowStepDto>(steps.Count);
                foreach (var s in steps)
                {
                    var rules = await _db.WorkflowRules.Where(r => r.WorkflowStepId == s.Id).ToListAsync(ct);
                    stepDtos.Add(new WorkflowStepDto(
                        s.Id, s.StepName, s.Sequence, s.ApprovalRoleId, roles.GetValueOrDefault(s.ApprovalRoleId, "(unknown role)"), s.IsMandatory,
                        rules.Select(r => new WorkflowRuleDto(r.Id, r.Attribute, r.Operator.ToString(), r.Value, r.Conjunction.ToString())).ToList()));
                }
                versionDtos.Add(new WorkflowVersionDto(v.Id, v.VersionNumber, v.Status.ToString(), v.EffectiveFrom, v.EffectiveTo, stepDtos));
            }

            result.Add(new WorkflowDefinitionDto(def.Id, def.Name, def.EntityType, def.Description, def.Status.ToString(), versionDtos));
        }

        return result;
    }

    public async Task<Guid> CreateDefinitionAsync(Guid createdBy, CreateWorkflowDefinitionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.EntityType))
        {
            throw new InvalidOperationException("A workflow needs a name and an entity type.");
        }
        if (await _db.WorkflowDefinitions.AnyAsync(d => d.EntityType == request.EntityType && d.Status == WorkflowDefinitionStatus.Active, ct))
        {
            throw new InvalidOperationException($"'{request.EntityType}' already has an active workflow - add a new version to it instead of creating another definition.");
        }

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var definition = new WorkflowDefinition
        {
            Name = request.Name, EntityType = request.EntityType, Description = request.Description,
            Status = WorkflowDefinitionStatus.Active, CreatedBy = createdBy, CreatedAtUtc = now
        };
        var version = new WorkflowVersion
        {
            WorkflowDefinitionId = definition.Id, VersionNumber = 1, EffectiveFrom = today, EffectiveTo = null,
            Status = WorkflowVersionStatus.Active, CreatedBy = createdBy, CreatedAtUtc = now
        };

        _db.WorkflowDefinitions.Add(definition);
        _db.WorkflowVersions.Add(version);
        await AddStepsAsync(version.Id, request.Steps, ct);

        await _db.SaveChangesAsync(ct);
        return definition.Id;
    }

    public async Task<Guid> CreateNewVersionAsync(Guid definitionId, Guid createdBy, CreateWorkflowVersionRequest request, CancellationToken ct = default)
    {
        var definition = await _db.WorkflowDefinitions.FindAsync([definitionId], ct)
            ?? throw new InvalidOperationException("Workflow definition not found.");

        var currentActive = await _db.WorkflowVersions
            .Where(v => v.WorkflowDefinitionId == definitionId && v.Status == WorkflowVersionStatus.Active)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        var nextVersionNumber = (await _db.WorkflowVersions
            .Where(v => v.WorkflowDefinitionId == definitionId)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(ct) ?? 0) + 1;

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        // Transactions already routed under currentActive keep evaluating against it -
        // WorkflowInstance stores the version id it started under, and that version's
        // steps/rules are never touched here, only retired for *future* lookups.
        if (currentActive is not null)
        {
            currentActive.Status = WorkflowVersionStatus.Retired;
            currentActive.EffectiveTo = today;
        }

        var newVersion = new WorkflowVersion
        {
            WorkflowDefinitionId = definitionId, VersionNumber = nextVersionNumber, EffectiveFrom = today, EffectiveTo = null,
            Status = WorkflowVersionStatus.Active, CreatedBy = createdBy, CreatedAtUtc = now
        };
        _db.WorkflowVersions.Add(newVersion);
        await AddStepsAsync(newVersion.Id, request.Steps, ct);

        definition.Status = WorkflowDefinitionStatus.Active;
        await _db.SaveChangesAsync(ct);
        return newVersion.Id;
    }

    private async Task AddStepsAsync(Guid versionId, IReadOnlyList<CreateWorkflowStepRequest> steps, CancellationToken ct)
    {
        if (steps.Count == 0)
        {
            throw new InvalidOperationException("A workflow needs at least one approval step.");
        }

        var roleIds = steps.Select(s => s.ApprovalRoleId).Distinct().ToList();
        var knownRoleIds = await _db.Roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Id).ToListAsync(ct);
        var unknownRole = roleIds.FirstOrDefault(id => !knownRoleIds.Contains(id));
        if (unknownRole != default)
        {
            throw new InvalidOperationException($"Role '{unknownRole}' does not exist in this organisation.");
        }

        foreach (var stepRequest in steps)
        {
            if (string.IsNullOrWhiteSpace(stepRequest.StepName))
            {
                throw new InvalidOperationException("Every step needs a name.");
            }

            var step = new WorkflowStep
            {
                WorkflowVersionId = versionId,
                StepCode = stepRequest.StepName.ToUpperInvariant().Replace(' ', '_'),
                StepName = stepRequest.StepName,
                Sequence = stepRequest.Sequence,
                StepType = "Approval",
                IsMandatory = stepRequest.IsMandatory,
                ApprovalRoleId = stepRequest.ApprovalRoleId
            };
            _db.WorkflowSteps.Add(step);

            foreach (var ruleRequest in stepRequest.Rules)
            {
                if (!Enum.TryParse<RuleOperator>(ruleRequest.Operator, out var op))
                {
                    throw new InvalidOperationException($"Unknown operator '{ruleRequest.Operator}'.");
                }
                if (!Enum.TryParse<RuleConjunction>(ruleRequest.Conjunction, out var conj))
                {
                    throw new InvalidOperationException($"Unknown conjunction '{ruleRequest.Conjunction}'.");
                }
                if (string.IsNullOrWhiteSpace(ruleRequest.Attribute) || string.IsNullOrWhiteSpace(ruleRequest.Value))
                {
                    throw new InvalidOperationException($"Step '{stepRequest.StepName}': every rule needs an attribute and a value.");
                }

                _db.WorkflowRules.Add(new WorkflowRule
                {
                    WorkflowStepId = step.Id, Attribute = ruleRequest.Attribute, Operator = op, Value = ruleRequest.Value, Conjunction = conj
                });
            }
        }
    }
}
