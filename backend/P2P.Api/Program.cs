using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using P2P.Api.MultiTenancy;
using P2P.Application.Abstractions;
using P2P.Domain.Organisation;
using P2P.Infrastructure.MultiTenancy;
using P2P.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// --- Multi-tenancy -----------------------------------------------------------------
// Config-backed registry today (see appsettings.json -> "Tenancy"); swapped for a
// platform.organisations-backed implementation once that schema exists.
builder.Services.Configure<TenancyOptions>(builder.Configuration.GetSection(TenancyOptions.SectionName));
builder.Services.AddScoped<IOrganisationRegistry, ConfigOrganisationRegistry>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

// --- Persistence ---------------------------------------------------------------------
// One connection string, N schemas: AppDbContext resolves which schema's tables to
// talk to from ITenantContext, and TenantModelCacheKeyFactory makes EF cache a
// distinct compiled model per schema instead of reusing the first tenant's model.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var tenant = sp.GetRequiredService<ITenantContext>();
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.ScopeMigrationsHistoryToTenant(tenant.SchemaName))
        .ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTimeOffset.UtcNow }));

// Diagnostic-only: proves the tenant resolver + schema-aware DbContext wiring end to
// end. Remove (or restrict to platform admins) once real modules replace it.
app.MapGet("/api/v1/_diagnostics/tenant", (ITenantContext tenant) => Results.Ok(new
{
    tenant.OrganisationId,
    tenant.OrgCode,
    tenant.SchemaName
}));

// Diagnostic-only: round-trips real rows through the tenant-scoped AppDbContext, so
// isolation can be proven against live Postgres, not just the resolver in isolation.
// Same two routes, same code path, different data per organisation.
app.MapGet("/api/v1/_diagnostics/legal-entities", async (AppDbContext db) =>
    Results.Ok(await db.LegalEntities
        .Select(e => new { e.Id, e.Code, e.Name, e.Country, e.BaseCurrency })
        .ToListAsync()));

app.MapPost("/api/v1/_diagnostics/legal-entities", async (AppDbContext db, CreateLegalEntityRequest body) =>
{
    var entity = new LegalEntity
    {
        Code = body.Code,
        Name = body.Name,
        Country = body.Country,
        BaseCurrency = body.BaseCurrency ?? "USD"
    };
    db.LegalEntities.Add(entity);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/_diagnostics/legal-entities/{entity.Id}", new { entity.Id, entity.Code, entity.Name });
});

app.Run();

record CreateLegalEntityRequest(string Code, string Name, string? Country, string? BaseCurrency);
