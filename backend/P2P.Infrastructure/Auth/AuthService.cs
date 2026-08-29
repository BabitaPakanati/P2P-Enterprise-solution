using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using P2P.Application.Auth;
using P2P.Domain.Identity;
using P2P.Infrastructure.Persistence;

namespace P2P.Infrastructure.Auth;

public sealed class AuthService : IAuthService
{
    private readonly PlatformDbContext _platform;
    private readonly IConfiguration _configuration;
    private readonly IJwtTokenService _tokenService;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthService(PlatformDbContext platform, IConfiguration configuration, IJwtTokenService tokenService)
    {
        _platform = platform;
        _configuration = configuration;
        _tokenService = tokenService;
    }

    public async Task<LoginResult> LoginAsync(string orgCode, string email, string password, CancellationToken ct = default)
    {
        var org = await _platform.Organisations.FirstOrDefaultAsync(o => o.OrgCode == orgCode, ct)
            ?? throw new InvalidOperationException("Unknown organisation.");

        // Login can't go through the normal DI-scoped, tenant-bound AppDbContext -
        // nothing has resolved a tenant yet (that's what logging in produces). Build
        // a one-off connection to this specific org's schema instead, exactly like
        // PlatformOrganisationProvisioner does for a schema that doesn't exist yet.
        var baseConnectionString = _configuration.GetConnectionString("Postgres")!;
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TenantConnectionStrings.ForSchema(baseConnectionString, org.SchemaName))
            .Options;
        await using var tenantDb = new AppDbContext(options);

        var user = await tenantDb.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);
        if (user?.PasswordHash is null)
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        var token = _tokenService.CreateToken(new TokenClaims(user.Id, user.Email, user.DisplayName, org.Id, org.OrgCode, org.SchemaName));
        return new LoginResult(token, user.Id, user.DisplayName, user.Email, org.Id, org.OrgCode, org.DisplayName);
    }
}
