namespace P2P.Application.Configuration;

/// <summary>
/// The enforcement half of field configuration - IFieldDefinitionService (Admin) is
/// where an org defines what fields exist; this is what every PurchaseRequisition/
/// PurchaseOrder create-or-update call runs the submitted values through before
/// anything is saved. One implementation, shared by every entity type, so a new
/// module wiring in custom fields later is a one-line call, not new validation logic.
/// </summary>
public interface ICustomFieldValidator
{
    /// <summary>
    /// Validates <paramref name="submitted"/> against this org's active field
    /// definitions for <paramref name="entityType"/> (required-ness, data type,
    /// dependency), and returns the JSON to store on the record. Throws
    /// InvalidOperationException with a specific, user-facing message on the first
    /// violation found - same convention as every other validation in the app.
    /// </summary>
    Task<string> ValidateAndSerializeAsync(string entityType, IReadOnlyDictionary<string, string>? submitted, CancellationToken ct = default);
}
