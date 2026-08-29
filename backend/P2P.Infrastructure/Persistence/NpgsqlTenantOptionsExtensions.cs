using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace P2P.Infrastructure.Persistence;

public static class NpgsqlTenantOptionsExtensions
{
    /// <summary>
    /// EF's own migrations-tracking table (<c>__EFMigrationsHistory</c>) has to live
    /// *inside* the tenant's schema too - not just the business tables. Left
    /// unqualified it defaults to `public` and is shared by every tenant, so applying
    /// the same migration to a second organisation looks "already applied" (the first
    /// org's row satisfies the idempotent script's history check) and silently
    /// no-ops. Scoping it here is what makes each org's migration history genuinely
    /// independent.
    /// </summary>
    public static NpgsqlDbContextOptionsBuilder ScopeMigrationsHistoryToTenant(
        this NpgsqlDbContextOptionsBuilder npgsqlOptions, string schemaName)
    {
        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", schemaName);
        return npgsqlOptions;
    }
}
