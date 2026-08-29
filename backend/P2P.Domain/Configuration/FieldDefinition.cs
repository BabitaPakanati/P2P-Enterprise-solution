using P2P.Domain.Common;

namespace P2P.Domain.Configuration;

public enum FieldDataType { Text, Number, Date, Boolean, Select }

/// <summary>
/// One org-configured field beyond the fixed ones every requisition/PO already has -
/// answers "for each org, what fields should be there apart from the basics, and is
/// each mandatory, what type, any dependency". Lives in the tenant schema (org-
/// specific config, same place WorkflowDefinition lives), scoped to one EntityType at
/// a time. The actual value a specific PurchaseRequisition/PurchaseOrder holds for
/// this field is not a column here - it's a key in that record's own
/// CustomFieldsJson, validated against these definitions on every create/update -
/// see CustomFieldValidator.
///
/// Deliberately NOT versioned the way WorkflowDefinition is: editing a field
/// definition in place is a real simplification relative to §61's "a configuration
/// change must itself be versioned and audited" (audited, via AuditLog, yes;
/// versioned, no). Workflow got the full treatment because an in-flight approval
/// depends on which version it started under; a field definition changing doesn't
/// retroactively invalidate anything already saved, so the risk profile is lower -
/// flagged here rather than silently assumed.
/// </summary>
public sealed class FieldDefinition : AuditableEntity
{
    public string EntityType { get; set; } = default!;
    public string FieldKey { get; set; } = default!;
    public string Label { get; set; } = default!;
    public FieldDataType DataType { get; set; }
    public bool IsRequired { get; set; }

    /// <summary>JSON array of strings, only meaningful when DataType == Select.</summary>
    public string? SelectOptionsJson { get; set; }

    /// <summary>This field only applies (shown, and required if IsRequired) when the field named here currently equals DependsOnValue.</summary>
    public string? DependsOnFieldKey { get; set; }
    public string? DependsOnValue { get; set; }

    public int Sequence { get; set; }
    public bool IsActive { get; set; } = true;
}
