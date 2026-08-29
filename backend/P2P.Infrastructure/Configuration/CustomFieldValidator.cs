using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using P2P.Application.Configuration;
using P2P.Domain.Configuration;
using P2P.Infrastructure.Persistence;

namespace P2P.Infrastructure.Configuration;

public sealed class CustomFieldValidator : ICustomFieldValidator
{
    private readonly AppDbContext _db;

    public CustomFieldValidator(AppDbContext db) => _db = db;

    public async Task<string> ValidateAndSerializeAsync(string entityType, IReadOnlyDictionary<string, string>? submitted, CancellationToken ct = default)
    {
        submitted ??= new Dictionary<string, string>();

        var definitions = await _db.FieldDefinitions
            .Where(f => f.EntityType == entityType && f.IsActive)
            .OrderBy(f => f.Sequence)
            .ToListAsync(ct);

        var output = new Dictionary<string, string>();

        foreach (var field in definitions)
        {
            var applies = true;
            if (!string.IsNullOrEmpty(field.DependsOnFieldKey))
            {
                // The field this one depends on might itself be a custom field
                // (already resolved into `submitted`) - only what was actually
                // submitted counts, not some other definition's default.
                applies = submitted.TryGetValue(field.DependsOnFieldKey, out var dependsOnValue)
                          && string.Equals(dependsOnValue, field.DependsOnValue, StringComparison.OrdinalIgnoreCase);
            }

            if (!applies)
            {
                continue; // don't validate, and don't persist a value for a field that isn't shown
            }

            var hasValue = submitted.TryGetValue(field.FieldKey, out var value) && !string.IsNullOrWhiteSpace(value);

            if (field.IsRequired && !hasValue)
            {
                throw new InvalidOperationException($"'{field.Label}' is required.");
            }
            if (!hasValue)
            {
                continue;
            }

            ValidateDataType(field, value!);
            output[field.FieldKey] = value!;
        }

        return JsonSerializer.Serialize(output);
    }

    private static void ValidateDataType(FieldDefinition field, string value)
    {
        switch (field.DataType)
        {
            case FieldDataType.Number:
                if (!decimal.TryParse(value, out _))
                {
                    throw new InvalidOperationException($"'{field.Label}' must be a number.");
                }
                break;
            case FieldDataType.Date:
                if (!DateOnly.TryParse(value, out _))
                {
                    throw new InvalidOperationException($"'{field.Label}' must be a valid date.");
                }
                break;
            case FieldDataType.Boolean:
                if (!bool.TryParse(value, out _))
                {
                    throw new InvalidOperationException($"'{field.Label}' must be true or false.");
                }
                break;
            case FieldDataType.Select:
                var options = string.IsNullOrEmpty(field.SelectOptionsJson)
                    ? []
                    : JsonSerializer.Deserialize<List<string>>(field.SelectOptionsJson) ?? [];
                if (!options.Contains(value))
                {
                    throw new InvalidOperationException($"'{field.Label}' must be one of: {string.Join(", ", options)}.");
                }
                break;
            case FieldDataType.Text:
            default:
                break;
        }
    }
}
