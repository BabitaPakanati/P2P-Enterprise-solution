namespace P2P.Application.Abstractions;

/// <summary>
/// The resolved tenant for the current unit of work (one HTTP request, or one
/// background job). Under schema-per-organisation tenancy, SchemaName is the only
/// thing that actually changes how a query behaves - it is what AppDbContext uses to
/// pick which Postgres schema's tables to talk to.
/// </summary>
public interface ITenantContext
{
    Guid OrganisationId { get; }
    string OrgCode { get; }
    string SchemaName { get; }
}

/// <summary>
/// Looks up an organisation's schema by its short code. The real implementation reads
/// the `platform.organisations` registry table; for local development before that
/// table exists, a config-backed implementation is registered instead (see
/// appsettings.json -> "Tenancy:Organisations").
/// </summary>
public interface IOrganisationRegistry
{
    Task<(Guid OrganisationId, string SchemaName)?> FindByCodeAsync(string orgCode, CancellationToken ct = default);
}
