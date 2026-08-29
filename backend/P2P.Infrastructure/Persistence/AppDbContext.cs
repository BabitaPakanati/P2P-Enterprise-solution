using Microsoft.EntityFrameworkCore;
using P2P.Application.Abstractions;
using P2P.Domain.Audit;
using P2P.Domain.Identity;
using P2P.Domain.Organisation;
using P2P.Domain.Procurement;
using P2P.Domain.Versioning;
using P2P.Domain.Workflow;

namespace P2P.Infrastructure.Persistence;

/// <summary>
/// The tenant-scoped DbContext. Every query and every generated migration is scoped
/// to exactly one organisation's schema - <see cref="TenantSchemaName"/> - resolved
/// once per request by <see cref="ITenantContext"/> and never overridden mid-request.
/// Table names are prefixed by module (organisation_*, identity_*, versioning_*,
/// audit_*, workflow_*) so the logical grouping the requirements call for is visible
/// directly in the table name, without needing a second layer of Postgres schemas
/// nested inside the tenant schema (Postgres schemas don't nest).
/// </summary>
public sealed class AppDbContext : DbContext
{
    public string TenantSchemaName { get; }

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        TenantSchemaName = tenantContext.SchemaName;
    }

    // Organisation
    public DbSet<LegalEntity> LegalEntities => Set<LegalEntity>();
    public DbSet<BusinessUnit> BusinessUnits => Set<BusinessUnit>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<Location> Locations => Set<Location>();

    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuthorityAssignment> AuthorityAssignments => Set<AuthorityAssignment>();
    public DbSet<Delegation> Delegations => Set<Delegation>();

    // Versioning
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();

    // Audit
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AuditFieldChange> AuditFieldChanges => Set<AuditFieldChange>();

    // Workflow
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowVersion> WorkflowVersions => Set<WorkflowVersion>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<WorkflowRule> WorkflowRules => Set<WorkflowRule>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<ApprovalTask> ApprovalTasks => Set<ApprovalTask>();

    // Procurement
    public DbSet<PurchaseRequisition> PurchaseRequisitions => Set<PurchaseRequisition>();
    public DbSet<PurchaseRequisitionLine> PurchaseRequisitionLines => Set<PurchaseRequisitionLine>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TenantSchemaName);

        modelBuilder.Entity<LegalEntity>().ToTable("organisation_legal_entity");
        modelBuilder.Entity<BusinessUnit>().ToTable("organisation_business_unit");
        modelBuilder.Entity<Department>().ToTable("organisation_department");
        modelBuilder.Entity<CostCenter>().ToTable("organisation_cost_center");
        modelBuilder.Entity<Location>().ToTable("organisation_location");

        modelBuilder.Entity<User>().ToTable("identity_user");
        modelBuilder.Entity<Role>().ToTable("identity_role");
        modelBuilder.Entity<Permission>().ToTable("identity_permission");
        modelBuilder.Entity<RolePermission>().ToTable("identity_role_permission");
        modelBuilder.Entity<AuthorityAssignment>().ToTable("identity_authority_assignment");
        modelBuilder.Entity<Delegation>().ToTable("identity_delegation");

        modelBuilder.Entity<Document>().ToTable("versioning_document");
        modelBuilder.Entity<DocumentVersion>().ToTable("versioning_document_version");

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.ToTable("audit_log");
            b.HasMany(a => a.FieldChanges).WithOne().HasForeignKey(fc => fc.AuditLogId);
        });
        modelBuilder.Entity<AuditFieldChange>().ToTable("audit_field_change");

        modelBuilder.Entity<WorkflowDefinition>().ToTable("workflow_definition");
        modelBuilder.Entity<WorkflowVersion>().ToTable("workflow_version");
        modelBuilder.Entity<WorkflowStep>().ToTable("workflow_step");
        modelBuilder.Entity<WorkflowRule>().ToTable("workflow_rule");
        modelBuilder.Entity<WorkflowInstance>().ToTable("workflow_instance");
        modelBuilder.Entity<ApprovalTask>().ToTable("workflow_approval_task");

        modelBuilder.Entity<PurchaseRequisition>(b =>
        {
            b.ToTable("procurement_purchase_requisition");
            b.HasMany(p => p.Lines).WithOne().HasForeignKey(l => l.PurchaseRequisitionId);
        });
        modelBuilder.Entity<PurchaseRequisitionLine>(b =>
        {
            b.ToTable("procurement_purchase_requisition_line");
            b.Ignore(l => l.EstimatedValue); // computed (Quantity * EstimatedUnitPrice), not stored
        });

        modelBuilder.Entity<PurchaseOrder>(b =>
        {
            b.ToTable("procurement_purchase_order");
            b.HasMany(p => p.Lines).WithOne().HasForeignKey(l => l.PurchaseOrderId);
        });
        modelBuilder.Entity<PurchaseOrderLine>(b =>
        {
            b.ToTable("procurement_purchase_order_line");
            b.Ignore(l => l.LineValue); // computed (Quantity * UnitPrice), not stored
        });
    }
}
