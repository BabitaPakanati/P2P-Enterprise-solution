using Microsoft.EntityFrameworkCore;
using P2P.Application.Admin;
using P2P.Domain.Identity;
using P2P.Infrastructure.Persistence;

namespace P2P.Infrastructure.Admin;

public sealed class RoleService : IRoleService
{
    private readonly AppDbContext _db;

    public RoleService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct = default) =>
        await _db.Roles
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Code, r.Name, r.Description))
            .ToListAsync(ct);

    public async Task<Guid> CreateAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Role code and name are required.");
        }
        if (await _db.Roles.AnyAsync(r => r.Code == request.Code, ct))
        {
            throw new InvalidOperationException($"A role with code '{request.Code}' already exists.");
        }

        var role = new Role { Code = request.Code, Name = request.Name, Description = request.Description };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);
        return role.Id;
    }
}
