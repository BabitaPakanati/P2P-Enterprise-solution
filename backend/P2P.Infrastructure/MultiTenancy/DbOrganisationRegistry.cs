using Microsoft.EntityFrameworkCore;
using P2P.Application.Abstractions;
using P2P.Infrastructure.Persistence;

namespace P2P.Infrastructure.MultiTenancy;

/// <summary>Backed by the real `platform.organisations` table - replaces the earlier appsettings.json stand-in.</summary>
public sealed class DbOrganisationRegistry : IOrganisationRegistry
{
    private readonly PlatformDbContext _platform;

    public DbOrganisationRegistry(PlatformDbContext platform) => _platform = platform;

    public async Task<(Guid OrganisationId, string SchemaName)?> FindByCodeAsync(string orgCode, CancellationToken ct = default)
    {
        var org = await _platform.Organisations
            .Where(o => o.OrgCode == orgCode)
            .Select(o => new { o.Id, o.SchemaName })
            .FirstOrDefaultAsync(ct);

        return org is null ? null : (org.Id, org.SchemaName);
    }
}
