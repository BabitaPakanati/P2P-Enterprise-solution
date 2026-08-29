using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using P2P.Api.Diagnostics;
using P2P.Api.MultiTenancy;
using P2P.Application.Abstractions;
using P2P.Application.Procurement;
using P2P.Application.Workflow;
using P2P.Domain.Organisation;
using P2P.Infrastructure.MultiTenancy;
using P2P.Infrastructure.Persistence;
using P2P.Infrastructure.Procurement;
using P2P.Infrastructure.Workflow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .SetIsOriginAllowed(_ => true) // local dev only - the frontend runs on a different port
    .AllowAnyHeader()
    .AllowAnyMethod()));

// --- Multi-tenancy -----------------------------------------------------------------
// Config-backed registry today (see appsettings.json -> "Tenancy"); swapped for a
// platform.organisations-backed implementation once that schema exists.
builder.Services.Configure<TenancyOptions>(builder.Configuration.GetSection(TenancyOptions.SectionName));
builder.Services.AddScoped<IOrganisationRegistry, ConfigOrganisationRegistry>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

builder.Services.AddScoped<CurrentUserContext>();
builder.Services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserContext>());

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

// --- Workflow + Procurement ------------------------------------------------------------
// The engine is entity-type agnostic; each module registers itself as both its own
// service interface AND an IWorkflowCompletionHandler, so the engine can call back
// into it without knowing it exists at compile time - see IWorkflowCompletionHandler.
builder.Services.AddScoped<IWorkflowEngine, WorkflowEngine>();
builder.Services.AddScoped<IApprovalService, ApprovalService>();

builder.Services.AddScoped<PurchaseRequisitionService>();
builder.Services.AddScoped<IPurchaseRequisitionService>(sp => sp.GetRequiredService<PurchaseRequisitionService>());
builder.Services.AddScoped<IWorkflowCompletionHandler>(sp => sp.GetRequiredService<PurchaseRequisitionService>());

builder.Services.AddScoped<PurchaseOrderService>();
builder.Services.AddScoped<IPurchaseOrderService>(sp => sp.GetRequiredService<PurchaseOrderService>());
builder.Services.AddScoped<IWorkflowCompletionHandler>(sp => sp.GetRequiredService<PurchaseOrderService>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

// Business-rule violations (InvalidOperationException, thrown deliberately by the
// services above for things like "only a Draft can be submitted") become a clear
// 400 with the message intact - not a stack trace. Matches §65's error-handling
// principle: actionable messages, not "Error 500".
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (InvalidOperationException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

app.UseHttpsRedirection();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<CurrentUserMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTimeOffset.UtcNow }));

// Diagnostic-only: proves the tenant resolver + schema-aware DbContext wiring end to
// end. Remove (or restrict to platform admins) once real modules replace it.
app.MapGet("/api/v1/_diagnostics/tenant", (ITenantContext tenant) => Results.Ok(new
{
    tenant.OrganisationId,
    tenant.OrgCode,
    tenant.SchemaName
}));

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

// Dev-only bootstrap for this vertical slice - see FoundationSeeder for what it
// creates and why it's not how a real organisation gets provisioned.
app.MapPost("/api/v1/_diagnostics/seed-foundation", async (AppDbContext db, ITenantContext tenant) =>
    Results.Ok(await FoundationSeeder.SeedAsync(db, tenant.OrgCode, default)));

// --- Requisitions ----------------------------------------------------------------------

app.MapPost("/api/v1/requisitions", async (IPurchaseRequisitionService svc, ICurrentUserContext user, CreateRequisitionRequest body) =>
{
    var id = await svc.CreateAsync(user.UserId, body);
    return Results.Created($"/api/v1/requisitions/{id}", new { id });
});

app.MapPost("/api/v1/requisitions/{id:guid}/submit", async (IPurchaseRequisitionService svc, Guid id) =>
{
    await svc.SubmitAsync(id);
    return Results.NoContent();
});

app.MapPost("/api/v1/requisitions/{id:guid}/cancel", async (IPurchaseRequisitionService svc, Guid id) =>
{
    await svc.CancelAsync(id);
    return Results.NoContent();
});

app.MapGet("/api/v1/requisitions", async (IPurchaseRequisitionService svc, ICurrentUserContext user, bool? mine) =>
    Results.Ok(await svc.ListAsync(mine == true ? user.UserId : null)));

app.MapGet("/api/v1/requisitions/{id:guid}", async (IPurchaseRequisitionService svc, Guid id) =>
{
    var dto = await svc.GetAsync(id);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
});

// --- Purchase Orders -------------------------------------------------------------------

app.MapPost("/api/v1/purchase-orders", async (IPurchaseOrderService svc, ICurrentUserContext user, CreatePurchaseOrderRequest body) =>
{
    var id = await svc.CreateFromRequisitionAsync(user.UserId, body);
    return Results.Created($"/api/v1/purchase-orders/{id}", new { id });
});

app.MapPost("/api/v1/purchase-orders/{id:guid}/submit", async (IPurchaseOrderService svc, Guid id) =>
{
    await svc.SubmitAsync(id);
    return Results.NoContent();
});

app.MapPost("/api/v1/purchase-orders/{id:guid}/send", async (IPurchaseOrderService svc, Guid id) =>
{
    await svc.SendToSupplierAsync(id);
    return Results.NoContent();
});

app.MapPost("/api/v1/purchase-orders/{id:guid}/amend", async (IPurchaseOrderService svc, ICurrentUserContext user, Guid id, AmendPurchaseOrderRequest body) =>
{
    await svc.AmendAsync(id, user.UserId, body);
    return Results.NoContent();
});

app.MapGet("/api/v1/purchase-orders", async (IPurchaseOrderService svc) => Results.Ok(await svc.ListAsync()));

app.MapGet("/api/v1/purchase-orders/{id:guid}", async (IPurchaseOrderService svc, Guid id) =>
{
    var dto = await svc.GetAsync(id);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
});

app.MapGet("/api/v1/purchase-orders/{id:guid}/versions", async (IPurchaseOrderService svc, Guid id) =>
    Results.Ok(await svc.GetVersionHistoryAsync(id)));

// --- Approvals -------------------------------------------------------------------------

app.MapGet("/api/v1/approvals/my", async (IApprovalService svc, ICurrentUserContext user) =>
    Results.Ok(await svc.GetMyPendingTasksAsync(user.UserId)));

app.MapPost("/api/v1/approvals/{taskId:guid}/decide", async (IApprovalService svc, ICurrentUserContext user, Guid taskId, DecideApprovalRequest body) =>
{
    await svc.DecideAsync(taskId, user.UserId, body.Approve, body.Comments);
    return Results.NoContent();
});

app.Run();

record CreateLegalEntityRequest(string Code, string Name, string? Country, string? BaseCurrency);
record DecideApprovalRequest(bool Approve, string? Comments);
