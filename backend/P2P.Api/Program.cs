using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using P2P.Api.Diagnostics;
using P2P.Api.MultiTenancy;
using P2P.Application.Abstractions;
using P2P.Application.Admin;
using P2P.Application.Auth;
using P2P.Application.Procurement;
using P2P.Application.Receiving;
using P2P.Application.Workflow;
using P2P.Domain.Organisation;
using P2P.Domain.Platform;
using P2P.Application.Configuration;
using P2P.Infrastructure.Admin;
using P2P.Infrastructure.Configuration;
using P2P.Infrastructure.Auth;
using P2P.Infrastructure.MultiTenancy;
using P2P.Infrastructure.Persistence;
using P2P.Infrastructure.Procurement;
using P2P.Infrastructure.Receiving;
using P2P.Infrastructure.Workflow;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .SetIsOriginAllowed(_ => true) // local dev only - the frontend runs on a different port
    .AllowAnyHeader()
    .AllowAnyMethod()));

// --- Multi-tenancy -----------------------------------------------------------------
// Real platform.organisations registry (see PlatformDbContext) - replaces the
// earlier appsettings.json-backed stand-in now that it exists.
builder.Services.AddScoped<IOrganisationRegistry, DbOrganisationRegistry>();
builder.Services.AddScoped<PlatformOrganisationProvisioner>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

builder.Services.AddScoped<CurrentUserContext>();
builder.Services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserContext>());

// --- Persistence ---------------------------------------------------------------------
// PlatformDbContext always talks to the `platform` schema. AppDbContext talks to
// whichever schema ITenantContext resolves for this request, via search_path on the
// connection string - see TenantConnectionStrings and AppDbContext's class comment
// for why that's simpler than the schema-baked-into-the-model approach it replaced.
var baseConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseNpgsql(TenantConnectionStrings.ForSchema(baseConnectionString, "platform")));

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var tenant = sp.GetRequiredService<ITenantContext>();
    options.UseNpgsql(TenantConnectionStrings.ForSchema(baseConnectionString, tenant.SchemaName));
});

// --- Auth ------------------------------------------------------------------------------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPlatformAuthService, PlatformAuthService>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is missing - set Jwt:SigningKey via user secrets.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Without this, the handler silently remaps short claim names ("sub", "name")
        // to legacy XML-Soap claim type URIs on validation, so a lookup for the
        // literal "sub" claim IdentityResolutionMiddleware writes finds nothing even
        // though the token is valid. Keep claim types exactly as JwtTokenService wrote them.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization(options =>
    options.AddPolicy("PlatformAdmin", p => p.RequireClaim("platform_admin", "true")));

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

builder.Services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();

// --- Org-level admin (roles, workflow configuration) ------------------------------------
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IWorkflowConfigService, WorkflowConfigService>();
builder.Services.AddScoped<IFieldDefinitionService, FieldDefinitionService>();
builder.Services.AddScoped<ICustomFieldValidator, CustomFieldValidator>();

var app = builder.Build();

// Ensure the `platform` schema itself exists and is migrated before anything else
// runs - unlike an org schema (created by PlatformOrganisationProvisioner on demand),
// this one has to be ready before a single request can resolve a tenant at all.
await using (var scope = app.Services.CreateAsyncScope())
{
    await using var conn = new Npgsql.NpgsqlConnection(baseConnectionString);
    await conn.OpenAsync();
    await using (var cmd = new Npgsql.NpgsqlCommand("CREATE SCHEMA IF NOT EXISTS \"platform\"", conn))
    {
        await cmd.ExecuteNonQueryAsync();
    }
    var platformDb = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    await platformDb.Database.MigrateAsync();
}

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
app.UseAuthentication();
app.UseMiddleware<IdentityResolutionMiddleware>(); // reads claims context.User now carries, or falls back to X-Org-Code for dev-only bootstrap paths
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTimeOffset.UtcNow })).AllowAnonymous();

// --- Auth --------------------------------------------------------------------------------

app.MapPost("/api/v1/auth/login", async (IAuthService auth, HttpContext ctx, LoginRequest body) =>
{
    if (!ctx.Request.Headers.TryGetValue("X-Org-Code", out var orgCode) || string.IsNullOrWhiteSpace(orgCode))
    {
        return Results.BadRequest(new { error = "Missing required header 'X-Org-Code'." });
    }
    var result = await auth.LoginAsync(orgCode!, body.Email, body.Password);
    return Results.Ok(result);
}).AllowAnonymous();

app.MapPost("/api/v1/platform/auth/login", async (IPlatformAuthService auth, PlatformLoginRequest body) =>
    Results.Ok(await auth.LoginAsync(body.Email, body.Password))).AllowAnonymous();

// Dev-only bootstrap - see PlatformAdminSeeder. A real deployment provisions the
// first root admin out-of-band, never via an open endpoint like this one.
app.MapPost("/api/v1/platform/_diagnostics/seed-admin", async (PlatformDbContext db) =>
    Results.Ok(await PlatformAdminSeeder.SeedAsync(db, default))).AllowAnonymous();

// --- Platform (org provisioning) ----------------------------------------------------------
// Gated behind the PlatformAdmin policy - a signed-in root admin only. Automates
// what was a manual dotnet-ef-script + psql ritual for every new organisation -
// see PlatformOrganisationProvisioner.

var platform = app.MapGroup("/api/v1/platform").RequireAuthorization("PlatformAdmin");

platform.MapGet("/organisations", async (PlatformDbContext db) =>
    Results.Ok(await db.Organisations
        .OrderBy(o => o.DisplayName)
        .Select(o => new { o.Id, o.OrgCode, o.DisplayName, o.SchemaName, Status = o.Status.ToString(), o.CreatedAtUtc })
        .ToListAsync()));

platform.MapPost("/organisations", async (PlatformOrganisationProvisioner provisioner, ProvisionOrgRequest body) =>
{
    var org = await provisioner.ProvisionAsync(body.OrgCode, body.DisplayName);
    return Results.Ok(new { org.Id, org.OrgCode, org.DisplayName, org.SchemaName, Status = org.Status.ToString() });
});

// --- Dev-only diagnostics ------------------------------------------------------------------

app.MapGet("/api/v1/_diagnostics/tenant", (ITenantContext tenant) => Results.Ok(new
{
    tenant.OrganisationId,
    tenant.OrgCode,
    tenant.SchemaName
})).AllowAnonymous();

app.MapPost("/api/v1/_diagnostics/seed-foundation", async (AppDbContext db, ITenantContext tenant) =>
    Results.Ok(await FoundationSeeder.SeedAsync(db, tenant.OrgCode, default))).AllowAnonymous();

var diagnostics = app.MapGroup("/api/v1/_diagnostics").RequireAuthorization();

diagnostics.MapGet("/legal-entities", async (AppDbContext db) =>
    Results.Ok(await db.LegalEntities
        .Select(e => new { e.Id, e.Code, e.Name, e.Country, e.BaseCurrency })
        .ToListAsync()));

diagnostics.MapPost("/legal-entities", async (AppDbContext db, CreateLegalEntityRequest body) =>
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

// --- Requisitions (all require a valid token) ---------------------------------------------

var requisitions = app.MapGroup("/api/v1/requisitions").RequireAuthorization();

requisitions.MapPost("/", async (IPurchaseRequisitionService svc, ICurrentUserContext user, CreateRequisitionRequest body) =>
{
    var id = await svc.CreateAsync(user.UserId, body);
    return Results.Created($"/api/v1/requisitions/{id}", new { id });
});

requisitions.MapPut("/{id:guid}", async (IPurchaseRequisitionService svc, Guid id, UpdateRequisitionRequest body) =>
{
    await svc.UpdateAsync(id, body);
    return Results.NoContent();
});

requisitions.MapPost("/{id:guid}/submit", async (IPurchaseRequisitionService svc, Guid id) =>
{
    await svc.SubmitAsync(id);
    return Results.NoContent();
});

requisitions.MapPost("/{id:guid}/cancel", async (IPurchaseRequisitionService svc, Guid id) =>
{
    await svc.CancelAsync(id);
    return Results.NoContent();
});

requisitions.MapPost("/{id:guid}/amend", async (IPurchaseRequisitionService svc, ICurrentUserContext user, Guid id, AmendRequisitionRequest body) =>
{
    await svc.AmendAsync(id, user.UserId, body);
    return Results.NoContent();
});

requisitions.MapGet("/", async (IPurchaseRequisitionService svc, ICurrentUserContext user, bool? mine) =>
    Results.Ok(await svc.ListAsync(mine == true ? user.UserId : null)));

requisitions.MapGet("/{id:guid}", async (IPurchaseRequisitionService svc, Guid id) =>
{
    var dto = await svc.GetAsync(id);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
});

requisitions.MapGet("/{id:guid}/versions", async (IPurchaseRequisitionService svc, Guid id) =>
    Results.Ok(await svc.GetVersionHistoryAsync(id)));

// --- Purchase Orders -----------------------------------------------------------------------

var purchaseOrders = app.MapGroup("/api/v1/purchase-orders").RequireAuthorization();

purchaseOrders.MapPost("/", async (IPurchaseOrderService svc, ICurrentUserContext user, CreatePurchaseOrderRequest body) =>
{
    var id = await svc.CreateFromRequisitionAsync(user.UserId, body);
    return Results.Created($"/api/v1/purchase-orders/{id}", new { id });
});

purchaseOrders.MapPost("/{id:guid}/submit", async (IPurchaseOrderService svc, Guid id) =>
{
    await svc.SubmitAsync(id);
    return Results.NoContent();
});

purchaseOrders.MapPost("/{id:guid}/send", async (IPurchaseOrderService svc, Guid id) =>
{
    await svc.SendToSupplierAsync(id);
    return Results.NoContent();
});

purchaseOrders.MapPost("/{id:guid}/amend", async (IPurchaseOrderService svc, ICurrentUserContext user, Guid id, AmendPurchaseOrderRequest body) =>
{
    await svc.AmendAsync(id, user.UserId, body);
    return Results.NoContent();
});

purchaseOrders.MapGet("/", async (IPurchaseOrderService svc) => Results.Ok(await svc.ListAsync()));

purchaseOrders.MapGet("/{id:guid}", async (IPurchaseOrderService svc, Guid id) =>
{
    var dto = await svc.GetAsync(id);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
});

purchaseOrders.MapGet("/{id:guid}/versions", async (IPurchaseOrderService svc, Guid id) =>
    Results.Ok(await svc.GetVersionHistoryAsync(id)));

purchaseOrders.MapGet("/{id:guid}/receipt-status", async (IGoodsReceiptService svc, Guid id) =>
    Results.Ok(await svc.GetReceiptStatusAsync(id)));

// --- Goods Receipts --------------------------------------------------------------------------

var goodsReceipts = app.MapGroup("/api/v1/goods-receipts").RequireAuthorization();

goodsReceipts.MapPost("/", async (IGoodsReceiptService svc, ICurrentUserContext user, CreateGoodsReceiptRequest body) =>
{
    var id = await svc.CreateAsync(user.UserId, body);
    return Results.Created($"/api/v1/goods-receipts/{id}", new { id });
});

goodsReceipts.MapPut("/{id:guid}", async (IGoodsReceiptService svc, Guid id, UpdateGoodsReceiptRequest body) =>
{
    await svc.UpdateAsync(id, body);
    return Results.NoContent();
});

goodsReceipts.MapPost("/{id:guid}/post", async (IGoodsReceiptService svc, Guid id) =>
{
    await svc.PostAsync(id);
    return Results.NoContent();
});

goodsReceipts.MapPost("/{id:guid}/cancel", async (IGoodsReceiptService svc, Guid id) =>
{
    await svc.CancelAsync(id);
    return Results.NoContent();
});

goodsReceipts.MapPost("/{id:guid}/amend", async (IGoodsReceiptService svc, ICurrentUserContext user, Guid id, AmendGoodsReceiptRequest body) =>
{
    await svc.AmendAsync(id, user.UserId, body);
    return Results.NoContent();
});

goodsReceipts.MapGet("/", async (IGoodsReceiptService svc, Guid? purchaseOrderId) => Results.Ok(await svc.ListAsync(purchaseOrderId)));

goodsReceipts.MapGet("/{id:guid}", async (IGoodsReceiptService svc, Guid id) =>
{
    var dto = await svc.GetAsync(id);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
});

goodsReceipts.MapGet("/{id:guid}/versions", async (IGoodsReceiptService svc, Guid id) =>
    Results.Ok(await svc.GetVersionHistoryAsync(id)));

// --- Approvals -------------------------------------------------------------------------------

var approvals = app.MapGroup("/api/v1/approvals").RequireAuthorization();

approvals.MapGet("/my", async (IApprovalService svc, ICurrentUserContext user) =>
    Results.Ok(await svc.GetMyPendingTasksAsync(user.UserId)));

approvals.MapPost("/{taskId:guid}/decide", async (IApprovalService svc, ICurrentUserContext user, Guid taskId, DecideApprovalRequest body) =>
{
    await svc.DecideAsync(taskId, user.UserId, body.Approve, body.Comments);
    return Results.NoContent();
});

// --- Org-level admin (roles, workflow configuration) --------------------------------------
// RequireAuthorization() only, no extra policy - there's no granular per-role
// permission check anywhere else in the app yet either (§52's RBAC/SoD requirements
// are modeled in the domain but not enforced at the API layer). Any signed-in org
// user can reach these for now; that's a real gap to close before production, same
// class of TODO as the platform-admin-only org provisioning endpoint had before it
// got a real policy.

var admin = app.MapGroup("/api/v1/admin").RequireAuthorization();

admin.MapGet("/roles", async (IRoleService svc) => Results.Ok(await svc.ListAsync()));
admin.MapPost("/roles", async (IRoleService svc, CreateRoleRequest body) => Results.Ok(new { Id = await svc.CreateAsync(body) }));

admin.MapGet("/workflows", async (IWorkflowConfigService svc) => Results.Ok(await svc.ListAsync()));

admin.MapPost("/workflows", async (IWorkflowConfigService svc, ICurrentUserContext user, CreateWorkflowDefinitionRequest body) =>
    Results.Ok(new { Id = await svc.CreateDefinitionAsync(user.UserId, body) }));

admin.MapPost("/workflows/{id:guid}/versions", async (IWorkflowConfigService svc, ICurrentUserContext user, Guid id, CreateWorkflowVersionRequest body) =>
    Results.Ok(new { Id = await svc.CreateNewVersionAsync(id, user.UserId, body) }));

admin.MapGet("/fields", async (IFieldDefinitionService svc, string? entityType) => Results.Ok(await svc.ListAsync(entityType)));

admin.MapPost("/fields", async (IFieldDefinitionService svc, CreateFieldDefinitionRequest body) =>
    Results.Ok(new { Id = await svc.CreateAsync(body) }));

admin.MapPut("/fields/{id:guid}", async (IFieldDefinitionService svc, Guid id, UpdateFieldDefinitionRequest body) =>
{
    await svc.UpdateAsync(id, body);
    return Results.NoContent();
});

admin.MapPost("/fields/{id:guid}/deactivate", async (IFieldDefinitionService svc, Guid id) =>
{
    await svc.DeactivateAsync(id);
    return Results.NoContent();
});

app.Run();

record CreateLegalEntityRequest(string Code, string Name, string? Country, string? BaseCurrency);
record DecideApprovalRequest(bool Approve, string? Comments);
record LoginRequest(string Email, string Password);
record PlatformLoginRequest(string Email, string Password);
record ProvisionOrgRequest(string OrgCode, string DisplayName);
