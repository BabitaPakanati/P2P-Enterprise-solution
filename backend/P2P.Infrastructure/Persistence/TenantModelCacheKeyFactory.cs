using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using P2P.Application.Abstractions;

namespace P2P.Infrastructure.Persistence;

/// <summary>
/// EF Core caches one compiled model per DbContext type by default - fine for a
/// single-schema app, wrong here, because <see cref="AppDbContext.OnModelCreating"/>
/// bakes the tenant's schema name into every table mapping via HasDefaultSchema.
/// Without this factory, the FIRST tenant to build the model would have its schema
/// silently reused for every other tenant afterwards.
///
/// Including the schema name in the cache key makes EF build (and cache) a distinct
/// model per distinct schema - one compiled model per organisation, each correctly
/// scoped to its own schema, reused across requests for that org.
/// </summary>
public sealed class TenantModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        var schema = (context as AppDbContext)?.TenantSchemaName ?? "__design_time__";
        return (context.GetType(), schema, designTime);
    }
}
