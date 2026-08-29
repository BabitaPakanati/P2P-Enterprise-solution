using Microsoft.Extensions.Options;
using P2P.Application.Abstractions;

namespace P2P.Infrastructure.MultiTenancy;

public sealed class OrganisationEntry
{
    public string OrgCode { get; set; } = default!;
    public Guid OrganisationId { get; set; }
    public string SchemaName { get; set; } = default!;
}

public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";
    public List<OrganisationEntry> Organisations { get; set; } = new();
}

/// <summary>
/// Development-time stand-in for the real registry. Reads the org -> schema map from
/// appsettings.json ("Tenancy:Organisations") instead of the `platform.organisations`
/// table, so tenant routing can be built and proven before the platform schema and
/// its own migrations exist. Swap this for a DB-backed implementation once that
/// schema is in place - callers only depend on <see cref="IOrganisationRegistry"/>.
/// </summary>
public sealed class ConfigOrganisationRegistry : IOrganisationRegistry
{
    private readonly TenancyOptions _options;

    public ConfigOrganisationRegistry(IOptions<TenancyOptions> options) => _options = options.Value;

    public Task<(Guid OrganisationId, string SchemaName)?> FindByCodeAsync(string orgCode, CancellationToken ct = default)
    {
        var match = _options.Organisations.FirstOrDefault(o =>
            string.Equals(o.OrgCode, orgCode, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult<(Guid, string)?>(
            match is null ? null : (match.OrganisationId, match.SchemaName));
    }
}
