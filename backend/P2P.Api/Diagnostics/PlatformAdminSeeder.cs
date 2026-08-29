using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using P2P.Domain.Platform;
using P2P.Infrastructure.Persistence;

namespace P2P.Api.Diagnostics;

public sealed record PlatformAdminSeedResult(Guid AdminId, string Email, string DevPassword, bool AlreadySeeded);

/// <summary>
/// Dev-only bootstrap, same rationale as FoundationSeeder: something has to create
/// the very first platform admin before anyone can log in as one. A real deployment
/// would provision this out-of-band (e.g. at infrastructure setup time), never via
/// an open HTTP endpoint - see the TODO already logged against org provisioning for
/// the same class of gap.
/// </summary>
public static class PlatformAdminSeeder
{
    public const string DevEmail = "root@platform.local";
    public const string DevPassword = "P2pRoot!2026";

    public static async Task<PlatformAdminSeedResult> SeedAsync(PlatformDbContext db, CancellationToken ct)
    {
        var existing = await db.AdminUsers.FirstOrDefaultAsync(a => a.Email == DevEmail, ct);
        if (existing is not null)
        {
            return new PlatformAdminSeedResult(existing.Id, existing.Email, DevPassword, AlreadySeeded: true);
        }

        var hasher = new PasswordHasher<PlatformAdminUser>();
        var admin = new PlatformAdminUser
        {
            Email = DevEmail,
            DisplayName = "Root Admin",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        admin.PasswordHash = hasher.HashPassword(admin, DevPassword);

        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync(ct);

        return new PlatformAdminSeedResult(admin.Id, admin.Email, DevPassword, AlreadySeeded: false);
    }
}
