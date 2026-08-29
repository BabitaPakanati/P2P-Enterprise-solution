using Microsoft.EntityFrameworkCore;
using P2P.Domain.Platform;

namespace P2P.Infrastructure.Persistence;

/// <summary>
/// Global, not tenant-scoped - always talks to the `platform` schema regardless of
/// which organisation is calling (its connection's search_path is fixed to
/// "platform", set once in Program.cs, never per-request). Deliberately tiny: this
/// is the one place an organisation is looked up by code, never by a human typing a
/// schema name.
/// </summary>
public sealed class PlatformDbContext : DbContext
{
    public PlatformDbContext(DbContextOptions<PlatformDbContext> options) : base(options) { }

    public DbSet<Organisation> Organisations => Set<Organisation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organisation>(b =>
        {
            b.ToTable("organisations");
            b.HasIndex(o => o.OrgCode).IsUnique();
            b.HasIndex(o => o.SchemaName).IsUnique();

            // The two schemas already provisioned by hand during Phase 0/1 development
            // (see docs/ARCHITECTURE.md) - registered here so they're real rows instead
            // of an appsettings.json stand-in, using the same ids they were seeded with
            // so existing data in org_acme / org_globex stays addressable.
            b.HasData(
                new
                {
                    Id = Guid.Parse("8f14e45f-ceea-4d5f-8f9b-000000000001"),
                    OrgCode = "acme", DisplayName = "Acme Corporation", SchemaName = "org_acme",
                    DeploymentTarget = DeploymentTarget.Aws, Status = OrganisationStatus.Active,
                    CreatedAtUtc = new DateTimeOffset(2026, 8, 29, 0, 0, 0, TimeSpan.Zero)
                },
                new
                {
                    Id = Guid.Parse("8f14e45f-ceea-4d5f-8f9b-000000000002"),
                    OrgCode = "globex", DisplayName = "Globex Corporation", SchemaName = "org_globex",
                    DeploymentTarget = DeploymentTarget.Aws, Status = OrganisationStatus.Active,
                    CreatedAtUtc = new DateTimeOffset(2026, 8, 29, 0, 0, 0, TimeSpan.Zero)
                });
        });
    }
}
