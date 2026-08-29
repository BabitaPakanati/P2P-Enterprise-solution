using Npgsql;

namespace P2P.Infrastructure.Persistence;

/// <summary>
/// The entire tenant-routing mechanism, in one method: point a connection's
/// search_path at the organisation's schema (falling back to `public` for shared
/// extensions), and every unqualified table name AppDbContext generates resolves
/// there automatically - no per-tenant EF model, no schema string baked into
/// migration code. See AppDbContext's class comment for what this replaced.
/// </summary>
public static class TenantConnectionStrings
{
    public static string ForSchema(string baseConnectionString, string schemaName)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = $"{schemaName},public"
        };
        return builder.ConnectionString;
    }
}
