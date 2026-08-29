using System.IdentityModel.Tokens.Jwt;
using P2P.Application.Abstractions;
using P2P.Infrastructure.MultiTenancy;

namespace P2P.Api.MultiTenancy;

/// <summary>
/// Populates TenantContext and CurrentUserContext for the request. For an
/// authenticated request that means reading the org_id/org_code/schema/sub claims a
/// login-issued JWT carries - see JwtTokenService. A handful of dev-only bootstrap
/// endpoints (seed-foundation, org provisioning) have no user yet to authenticate as,
/// so they fall back to resolving tenant from an X-Org-Code header exactly like the
/// whole API used to; every other endpoint is behind RequireAuthorization() and gets
/// a standard 401 from UseAuthorization() if it reaches this middleware unauthenticated.
/// </summary>
public sealed class IdentityResolutionMiddleware
{
    private const string OrgCodeHeader = "X-Org-Code";
    private readonly RequestDelegate _next;

    public IdentityResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IOrganisationRegistry registry, TenantContext tenantContext, CurrentUserContext currentUser)
    {
        if (IsPublicPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var orgId = context.User.FindFirst("org_id")?.Value;
            var orgCode = context.User.FindFirst("org_code")?.Value;
            var schema = context.User.FindFirst("schema")?.Value;
            var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (orgId is null || orgCode is null || schema is null || userId is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Token is missing required organisation/user claims." });
                return;
            }

            tenantContext.Set(Guid.Parse(orgId), orgCode, schema);
            currentUser.Set(Guid.Parse(userId));
            await _next(context);
            return;
        }

        if (IsAnonymousTenantScopedPath(context.Request.Path))
        {
            if (!context.Request.Headers.TryGetValue(OrgCodeHeader, out var headerOrgCode) || string.IsNullOrWhiteSpace(headerOrgCode))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = $"Missing required header '{OrgCodeHeader}'." });
                return;
            }

            var resolved = await registry.FindByCodeAsync(headerOrgCode!, context.RequestAborted);
            if (resolved is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(new { error = $"Unknown organisation '{headerOrgCode}'." });
                return;
            }

            tenantContext.Set(resolved.Value.OrganisationId, headerOrgCode!, resolved.Value.SchemaName);
            await _next(context);
            return;
        }

        // Unauthenticated request to a protected path: let it through untouched.
        // UseAuthorization (immediately after this middleware) rejects it with a
        // standard 401 for any endpoint that calls RequireAuthorization().
        await _next(context);
    }

    /// <summary>
    /// Paths that need no tenant at all - either there's no tenant yet
    /// (provisioning a new one) or the caller supplies org context some other way
    /// (login reads X-Org-Code itself, to pick which schema to check credentials
    /// against, without this middleware needing to resolve a full TenantContext for
    /// an unauthenticated caller).
    /// </summary>
    private static bool IsPublicPath(PathString path) =>
        path.StartsWithSegments("/health") ||
        path.StartsWithSegments("/openapi") ||
        path.StartsWithSegments("/api/v1/auth/login") ||
        path.StartsWithSegments("/api/v1/platform/organisations");

    /// <summary>
    /// Dev-only bootstrap endpoints that need a tenant resolved but have no user to
    /// authenticate as yet.
    /// </summary>
    private static bool IsAnonymousTenantScopedPath(PathString path) =>
        path.StartsWithSegments("/api/v1/_diagnostics/seed-foundation") ||
        path.StartsWithSegments("/api/v1/_diagnostics/tenant");
}
