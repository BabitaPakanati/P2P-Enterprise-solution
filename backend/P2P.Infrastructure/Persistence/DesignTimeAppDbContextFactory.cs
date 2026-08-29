using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace P2P.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` build the model without a running app. Now that
/// AppDbContext's model is schema-agnostic (search_path does the routing - see its
/// class comment), the connection here doesn't need to point at any particular
/// tenant; migrations generated against it apply unmodified to every organisation's
/// schema via PlatformOrganisationProvisioner.
/// </summary>
public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=p2p_design_time;Username=postgres;Password=postgres");

        return new AppDbContext(optionsBuilder.Options);
    }
}
