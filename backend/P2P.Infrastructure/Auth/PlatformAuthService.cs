using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using P2P.Application.Auth;
using P2P.Domain.Platform;
using P2P.Infrastructure.Persistence;

namespace P2P.Infrastructure.Auth;

public sealed class PlatformAuthService : IPlatformAuthService
{
    private readonly PlatformDbContext _platform;
    private readonly IJwtTokenService _tokenService;
    private readonly PasswordHasher<PlatformAdminUser> _passwordHasher = new();

    public PlatformAuthService(PlatformDbContext platform, IJwtTokenService tokenService)
    {
        _platform = platform;
        _tokenService = tokenService;
    }

    public async Task<PlatformLoginResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var admin = await _platform.AdminUsers.FirstOrDefaultAsync(a => a.Email == email && a.IsActive, ct);
        if (admin is null || _passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        var token = _tokenService.CreatePlatformAdminToken(new PlatformAdminTokenClaims(admin.Id, admin.Email, admin.DisplayName));
        return new PlatformLoginResult(token, admin.Id, admin.DisplayName, admin.Email);
    }
}
