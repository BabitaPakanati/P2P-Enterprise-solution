using P2P.Infrastructure.MultiTenancy;

namespace P2P.Api.MultiTenancy;

/// <summary>
/// Resolves the acting user from an X-User-Id header - a stand-in for a JWT claim,
/// exactly parallel to TenantResolutionMiddleware. Must run after tenant resolution
/// (it doesn't touch the database, but conceptually the user is scoped to the org).
/// </summary>
public sealed class CurrentUserMiddleware
{
    private const string UserIdHeader = "X-User-Id";
    private readonly RequestDelegate _next;

    public CurrentUserMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, CurrentUserContext currentUser)
    {
        if (IsExemptPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(UserIdHeader, out var raw) || !Guid.TryParse(raw, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = $"Missing or invalid required header '{UserIdHeader}' (expected a GUID)." });
            return;
        }

        currentUser.Set(userId);
        await _next(context);
    }

    private static bool IsExemptPath(PathString path) =>
        path.StartsWithSegments("/health") || path.StartsWithSegments("/openapi") ||
        path.StartsWithSegments("/api/v1/_diagnostics/tenant") || path.StartsWithSegments("/api/v1/_diagnostics/seed-foundation");
}
