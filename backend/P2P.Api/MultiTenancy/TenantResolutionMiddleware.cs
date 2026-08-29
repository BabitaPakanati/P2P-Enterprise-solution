using P2P.Application.Abstractions;
using P2P.Infrastructure.MultiTenancy;

namespace P2P.Api.MultiTenancy;

/// <summary>
/// Resolves the calling organisation and sets <see cref="TenantContext"/> before
/// anything downstream touches the database. Reads the org code from an
/// <c>X-Org-Code</c> header for now - a stand-in for the claim a real JWT will carry
/// once auth is wired in. Any request for an unknown org code is rejected here, not
/// left to fail later inside a query.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private const string OrgCodeHeader = "X-Org-Code";
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IOrganisationRegistry registry, TenantContext tenantContext)
    {
        if (IsExemptPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(OrgCodeHeader, out var orgCode) || string.IsNullOrWhiteSpace(orgCode))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = $"Missing required header '{OrgCodeHeader}'." });
            return;
        }

        var resolved = await registry.FindByCodeAsync(orgCode!, context.RequestAborted);
        if (resolved is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = $"Unknown organisation '{orgCode}'." });
            return;
        }

        tenantContext.Set(resolved.Value.OrganisationId, orgCode!, resolved.Value.SchemaName);
        await _next(context);
    }

    private static bool IsExemptPath(PathString path) =>
        path.StartsWithSegments("/health") || path.StartsWithSegments("/openapi");
}
