using P2P.Domain.Common;

namespace P2P.Domain.Platform;

public enum OrganisationStatus { Provisioning, Active, Suspended }
public enum DeploymentTarget { Aws, OnPrem }

/// <summary>
/// The one row that exists outside every tenant schema - see docs/ARCHITECTURE.md
/// §2's tenant-routing diagram. Lives in the `platform` schema, resolved once per
/// request to find which org_&lt;code&gt; schema a connection's search_path should
/// point at. Replaces the earlier appsettings.json-backed stand-in
/// (ConfigOrganisationRegistry) now that the real thing exists.
/// </summary>
public sealed class Organisation : Entity
{
    public string OrgCode { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string SchemaName { get; set; } = default!;
    public DeploymentTarget DeploymentTarget { get; set; } = DeploymentTarget.Aws;
    public OrganisationStatus Status { get; set; } = OrganisationStatus.Provisioning;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
