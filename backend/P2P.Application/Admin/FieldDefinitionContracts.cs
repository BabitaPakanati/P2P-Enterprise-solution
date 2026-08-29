namespace P2P.Application.Admin;

public sealed record CreateFieldDefinitionRequest(
    string EntityType, string FieldKey, string Label, string DataType, bool IsRequired,
    IReadOnlyList<string>? SelectOptions, string? DependsOnFieldKey, string? DependsOnValue, int Sequence);

/// <summary>Same shape as create - editing a field definition replaces it outright (not versioned - see FieldDefinition's class comment for why that's an accepted simplification here, unlike workflow).</summary>
public sealed record UpdateFieldDefinitionRequest(
    string Label, string DataType, bool IsRequired,
    IReadOnlyList<string>? SelectOptions, string? DependsOnFieldKey, string? DependsOnValue, int Sequence);

public sealed record FieldDefinitionDto(
    Guid Id, string EntityType, string FieldKey, string Label, string DataType, bool IsRequired,
    IReadOnlyList<string>? SelectOptions, string? DependsOnFieldKey, string? DependsOnValue, int Sequence, bool IsActive);

/// <summary>
/// Admin surface for "what fields should be there apart from the basics, for each
/// org, for each transaction type" - CRUD over FieldDefinition. Deactivate rather
/// than delete: existing records may already carry a value for this key in their
/// CustomFieldsJson, and deleting the definition shouldn't make that value
/// unreadable, just stop asking for it on new/edited records.
/// </summary>
public interface IFieldDefinitionService
{
    Task<IReadOnlyList<FieldDefinitionDto>> ListAsync(string? entityType = null, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateFieldDefinitionRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateFieldDefinitionRequest request, CancellationToken ct = default);
    Task DeactivateAsync(Guid id, CancellationToken ct = default);
}
