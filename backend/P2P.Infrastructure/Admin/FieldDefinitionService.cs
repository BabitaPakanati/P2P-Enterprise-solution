using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using P2P.Application.Admin;
using P2P.Domain.Configuration;
using P2P.Infrastructure.Persistence;

namespace P2P.Infrastructure.Admin;

public sealed class FieldDefinitionService : IFieldDefinitionService
{
    private static readonly Regex ValidFieldKey = new("^[a-zA-Z][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    private readonly AppDbContext _db;

    public FieldDefinitionService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<FieldDefinitionDto>> ListAsync(string? entityType = null, CancellationToken ct = default)
    {
        var query = _db.FieldDefinitions.AsQueryable();
        if (!string.IsNullOrEmpty(entityType)) query = query.Where(f => f.EntityType == entityType);

        return await query
            .OrderBy(f => f.EntityType).ThenBy(f => f.Sequence)
            .Select(f => ToDto(f))
            .ToListAsync(ct);
    }

    public async Task<Guid> CreateAsync(CreateFieldDefinitionRequest request, CancellationToken ct = default)
    {
        var dataType = ParseDataType(request.DataType);
        ValidateKeyAndOptions(request.FieldKey, dataType, request.SelectOptions);

        if (await _db.FieldDefinitions.AnyAsync(f => f.EntityType == request.EntityType && f.FieldKey == request.FieldKey, ct))
        {
            throw new InvalidOperationException($"A field with key '{request.FieldKey}' already exists for {request.EntityType}.");
        }
        await ValidateDependencyAsync(request.EntityType, request.DependsOnFieldKey, ct);

        var field = new FieldDefinition
        {
            EntityType = request.EntityType,
            FieldKey = request.FieldKey,
            Label = request.Label,
            DataType = dataType,
            IsRequired = request.IsRequired,
            SelectOptionsJson = request.SelectOptions is { Count: > 0 } ? JsonSerializer.Serialize(request.SelectOptions) : null,
            DependsOnFieldKey = string.IsNullOrWhiteSpace(request.DependsOnFieldKey) ? null : request.DependsOnFieldKey,
            DependsOnValue = string.IsNullOrWhiteSpace(request.DependsOnValue) ? null : request.DependsOnValue,
            Sequence = request.Sequence,
            IsActive = true
        };
        _db.FieldDefinitions.Add(field);
        await _db.SaveChangesAsync(ct);
        return field.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateFieldDefinitionRequest request, CancellationToken ct = default)
    {
        var field = await _db.FieldDefinitions.FindAsync([id], ct) ?? throw new InvalidOperationException("Field not found.");
        var dataType = ParseDataType(request.DataType);
        ValidateKeyAndOptions(field.FieldKey, dataType, request.SelectOptions);
        await ValidateDependencyAsync(field.EntityType, request.DependsOnFieldKey, ct, excludeFieldKey: field.FieldKey);

        field.Label = request.Label;
        field.DataType = dataType;
        field.IsRequired = request.IsRequired;
        field.SelectOptionsJson = request.SelectOptions is { Count: > 0 } ? JsonSerializer.Serialize(request.SelectOptions) : null;
        field.DependsOnFieldKey = string.IsNullOrWhiteSpace(request.DependsOnFieldKey) ? null : request.DependsOnFieldKey;
        field.DependsOnValue = string.IsNullOrWhiteSpace(request.DependsOnValue) ? null : request.DependsOnValue;
        field.Sequence = request.Sequence;

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var field = await _db.FieldDefinitions.FindAsync([id], ct) ?? throw new InvalidOperationException("Field not found.");
        field.IsActive = false;
        await _db.SaveChangesAsync(ct);
    }

    private async Task ValidateDependencyAsync(string entityType, string? dependsOnFieldKey, CancellationToken ct, string? excludeFieldKey = null)
    {
        if (string.IsNullOrWhiteSpace(dependsOnFieldKey)) return;
        if (dependsOnFieldKey == excludeFieldKey)
        {
            throw new InvalidOperationException("A field cannot depend on itself.");
        }
        if (!await _db.FieldDefinitions.AnyAsync(f => f.EntityType == entityType && f.FieldKey == dependsOnFieldKey && f.IsActive, ct))
        {
            throw new InvalidOperationException($"Dependency field '{dependsOnFieldKey}' does not exist or is not active.");
        }
    }

    private static void ValidateKeyAndOptions(string fieldKey, FieldDataType dataType, IReadOnlyList<string>? selectOptions)
    {
        if (!ValidFieldKey.IsMatch(fieldKey))
        {
            throw new InvalidOperationException("Field key must start with a letter and contain only letters, digits, or underscores.");
        }
        if (dataType == FieldDataType.Select && (selectOptions is null || selectOptions.Count == 0))
        {
            throw new InvalidOperationException("A Select field needs at least one option.");
        }
    }

    private static FieldDataType ParseDataType(string dataType) =>
        Enum.TryParse<FieldDataType>(dataType, out var parsed) ? parsed : throw new InvalidOperationException($"Unknown data type '{dataType}'.");

    private static FieldDefinitionDto ToDto(FieldDefinition f) => new(
        f.Id, f.EntityType, f.FieldKey, f.Label, f.DataType.ToString(), f.IsRequired,
        string.IsNullOrEmpty(f.SelectOptionsJson) ? null : JsonSerializer.Deserialize<List<string>>(f.SelectOptionsJson),
        f.DependsOnFieldKey, f.DependsOnValue, f.Sequence, f.IsActive);
}
