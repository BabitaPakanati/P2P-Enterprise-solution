using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using P2P.Domain.Platform;
using P2P.Infrastructure.Persistence;

namespace P2P.Infrastructure.MultiTenancy;

/// <summary>
/// Automates what was, through the end of Phase 1, a manual three-step ritual for
/// every new organisation: generate an idempotent migration script, text-substitute
/// the schema name into it, apply it with psql. That only worked because AppDbContext
/// used to bake a schema name into its compiled model; now that schema routing is a
/// pure connection-string concern (search_path - see TenantConnectionStrings), the
/// exact same migration set that created org_acme applies unmodified to any new
/// schema via an ordinary Database.MigrateAsync() call.
/// </summary>
public sealed class PlatformOrganisationProvisioner
{
    private static readonly Regex ValidOrgCode = new("^[a-z][a-z0-9_]{1,30}$", RegexOptions.Compiled);

    private readonly PlatformDbContext _platform;
    private readonly IConfiguration _configuration;

    public PlatformOrganisationProvisioner(PlatformDbContext platform, IConfiguration configuration)
    {
        _platform = platform;
        _configuration = configuration;
    }

    /// <summary>
    /// Idempotent by design, not just by accident: both CREATE SCHEMA IF NOT EXISTS
    /// and Database.MigrateAsync() are safe to call repeatedly, so "provision" here
    /// means "ensure this org is fully set up" rather than "create, and fail if it
    /// already is". That matters in practice - e.g. platform.organisations can carry
    /// a row (via migration seed data, or a prior partial failure) whose Postgres
    /// schema doesn't exist yet; calling this again just finishes the job instead of
    /// erroring on "already exists".
    /// </summary>
    public async Task<Organisation> ProvisionAsync(string orgCode, string displayName, CancellationToken ct = default)
    {
        if (!ValidOrgCode.IsMatch(orgCode))
        {
            throw new InvalidOperationException("Org code must be lowercase letters, digits, or underscores, starting with a letter.");
        }

        var org = await _platform.Organisations.FirstOrDefaultAsync(o => o.OrgCode == orgCode, ct);
        if (org is null)
        {
            org = new Organisation
            {
                OrgCode = orgCode, DisplayName = displayName, SchemaName = $"org_{orgCode}",
                Status = OrganisationStatus.Provisioning, CreatedAtUtc = DateTimeOffset.UtcNow
            };
            _platform.Organisations.Add(org);
            await _platform.SaveChangesAsync(ct);
        }

        var schemaName = org.SchemaName;
        var baseConnectionString = _configuration.GetConnectionString("Postgres")!;

        await using (var conn = new NpgsqlConnection(baseConnectionString))
        {
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\"", conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var tenantOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TenantConnectionStrings.ForSchema(baseConnectionString, schemaName))
            .Options;
        await using (var tenantDb = new AppDbContext(tenantOptions))
        {
            await tenantDb.Database.MigrateAsync(ct);
        }

        org.Status = OrganisationStatus.Active;
        await _platform.SaveChangesAsync(ct);
        return org;
    }
}
