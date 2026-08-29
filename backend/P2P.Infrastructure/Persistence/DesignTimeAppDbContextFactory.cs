using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using P2P.Application.Abstractions;

namespace P2P.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` build the model without a running app or a live
/// tenant. Migrations generated this way are scoped to a fixed placeholder schema
/// (org_template); applying the same DDL to a real organisation's schema at
/// provisioning time is a separate, deliberately-not-yet-built step - see
/// docs/ARCHITECTURE.md, "Next steps".
/// </summary>
public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var schema = Environment.GetEnvironmentVariable("P2P_DESIGN_TIME_SCHEMA") ?? "org_template";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder
            .UseNpgsql(
                "Host=localhost;Database=p2p_design_time;Username=postgres;Password=postgres",
                npgsql => npgsql.ScopeMigrationsHistoryToTenant(schema))
            .ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, TenantModelCacheKeyFactory>();

        return new AppDbContext(optionsBuilder.Options, new DesignTimeTenantContext(schema));
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public DesignTimeTenantContext(string schemaName) => SchemaName = schemaName;
        public Guid OrganisationId => Guid.Empty;
        public string OrgCode => "template";
        public string SchemaName { get; }
    }
}
