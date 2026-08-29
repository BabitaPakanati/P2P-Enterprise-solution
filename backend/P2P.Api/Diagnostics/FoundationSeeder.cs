using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using P2P.Domain.Identity;
using P2P.Domain.Workflow;
using P2P.Infrastructure.Persistence;

namespace P2P.Api.Diagnostics;

public sealed record SeedResult(Guid RequesterId, Guid ApproverId, Guid RoleId, bool AlreadySeeded, string DevPassword);

/// <summary>
/// Dev-only bootstrap: creates one role, one requester, one approver, an authority
/// assignment, and a single-step approval workflow for both PurchaseRequisition and
/// PurchaseOrder, scoped to whichever org schema the caller's X-Org-Code resolves to.
/// A real system provisions this through Administration screens (§37) driven by an
/// actual org admin, not a diagnostic endpoint - this exists purely so the vertical
/// slice has something to approve against without hand-writing SQL per schema.
/// Idempotent: safe to call more than once per organisation.
/// </summary>
public static class FoundationSeeder
{
    private const string ManagerRoleCode = "DEPT_MANAGER";

    /// <summary>
    /// Every seeded user gets this password. Fine for a dev bootstrap that creates
    /// throwaway demo accounts; would never fly for a real organisation - a real
    /// provisioning flow (see PlatformOrganisationProvisioner) would invite an admin
    /// by email and let them set their own password, never mint one.
    /// </summary>
    public const string DevPassword = "P2pDemo!2026";

    public static async Task<SeedResult> SeedAsync(AppDbContext db, string orgCode, CancellationToken ct)
    {
        var existingRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == ManagerRoleCode, ct);
        if (existingRole is not null)
        {
            var existingApprover = await db.AuthorityAssignments
                .Where(a => a.RoleId == existingRole.Id)
                .Select(a => a.UserId)
                .FirstAsync(ct);
            var existingRequester = await db.Users.Where(u => u.Email == $"requester@{orgCode}.example").Select(u => u.Id).FirstAsync(ct);
            return new SeedResult(existingRequester, existingApprover, existingRole.Id, AlreadySeeded: true, DevPassword);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;
        var hasher = new PasswordHasher<User>();

        var role = new Role { Code = ManagerRoleCode, Name = "Department Manager", Description = "Approves requisitions and purchase orders for their department." };
        var requester = new User { Email = $"requester@{orgCode}.example", DisplayName = "Priya Sharma", CreatedAtUtc = now };
        var approver = new User { Email = $"approver@{orgCode}.example", DisplayName = "Karan Mehta", CreatedAtUtc = now };
        requester.PasswordHash = hasher.HashPassword(requester, DevPassword);
        approver.PasswordHash = hasher.HashPassword(approver, DevPassword);
        requester.CreatedBy = requester.Id;
        approver.CreatedBy = approver.Id;

        var authority = new AuthorityAssignment
        {
            RoleId = role.Id,
            UserId = approver.Id,
            AmountFrom = 0,
            AmountTo = 100_000_000,
            EffectiveFrom = today,
            EffectiveTo = null,
            Priority = 1,
            Status = AuthorityAssignmentStatus.Active,
            CreatedBy = approver.Id,
            CreatedAtUtc = now
        };

        db.Roles.Add(role);
        db.Users.AddRange(requester, approver);
        db.AuthorityAssignments.Add(authority);

        foreach (var entityType in new[] { "PurchaseRequisition", "PurchaseOrder" })
        {
            var definition = new WorkflowDefinition
            {
                Name = $"{entityType} Approval", EntityType = entityType, Status = WorkflowDefinitionStatus.Active,
                CreatedBy = approver.Id, CreatedAtUtc = now
            };
            var version = new WorkflowVersion
            {
                WorkflowDefinitionId = definition.Id, VersionNumber = 1, EffectiveFrom = today, EffectiveTo = null,
                Status = WorkflowVersionStatus.Active, CreatedBy = approver.Id, CreatedAtUtc = now
            };
            var step = new WorkflowStep
            {
                WorkflowVersionId = version.Id, StepCode = "MANAGER_APPROVAL", StepName = "Manager Approval",
                Sequence = 1, StepType = "Approval", IsMandatory = true, ApprovalRoleId = role.Id
            };
            var rule = new WorkflowRule
            {
                WorkflowStepId = step.Id, Attribute = "Amount", Operator = RuleOperator.LessOrEqual, Value = "100000000", Conjunction = RuleConjunction.And
            };
            db.WorkflowDefinitions.Add(definition);
            db.WorkflowVersions.Add(version);
            db.WorkflowSteps.Add(step);
            db.WorkflowRules.Add(rule);
        }

        await db.SaveChangesAsync(ct);
        return new SeedResult(requester.Id, approver.Id, role.Id, AlreadySeeded: false, DevPassword);
    }
}
