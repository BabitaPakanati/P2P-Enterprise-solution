using P2P.Domain.Common;

namespace P2P.Domain.Audit;

/// <summary>
/// One row per material action. Properties are get-only and only ever set through
/// <see cref="Create"/> - there is no way to mutate an AuditLog once constructed.
/// That is a domain-level backstop; the real guarantee is the database grant (the
/// application's DB role has INSERT but never UPDATE/DELETE on the audit schema).
/// </summary>
public sealed class AuditLog : Entity
{
    public string EntityType { get; private set; } = default!;
    public Guid EntityId { get; private set; }
    public Guid? EntityVersionId { get; private set; }
    public string Action { get; private set; } = default!;
    public Guid UserId { get; private set; }
    public string UserName { get; private set; } = default!;
    public Guid? RoleId { get; private set; }
    public string? RoleName { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? SessionId { get; private set; }
    public string? RequestId { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTimeOffset TimestampUtc { get; private set; }
    public string? Source { get; private set; }
    public string? Reason { get; private set; }
    public string? Comments { get; private set; }

    private readonly List<AuditFieldChange> _fieldChanges = new();
    public IReadOnlyCollection<AuditFieldChange> FieldChanges => _fieldChanges.AsReadOnly();

    private AuditLog() { }

    public static AuditLog Create(
        string entityType, Guid entityId, string action,
        Guid userId, string userName,
        Guid? entityVersionId = null, Guid? roleId = null, string? roleName = null,
        string? ipAddress = null, string? userAgent = null, string? sessionId = null,
        string? requestId = null, string? correlationId = null,
        string? source = null, string? reason = null, string? comments = null)
    {
        return new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            EntityVersionId = entityVersionId,
            Action = action,
            UserId = userId,
            UserName = userName,
            RoleId = roleId,
            RoleName = roleName,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            SessionId = sessionId,
            RequestId = requestId,
            CorrelationId = correlationId,
            TimestampUtc = DateTimeOffset.UtcNow,
            Source = source,
            Reason = reason,
            Comments = comments
        };
    }

    public void RecordFieldChange(string fieldName, string? oldValue, string? newValue, string dataType)
        => _fieldChanges.Add(AuditFieldChange.Create(Id, fieldName, oldValue, newValue, dataType));
}

public sealed class AuditFieldChange : Entity
{
    public Guid AuditLogId { get; private set; }
    public string FieldName { get; private set; } = default!;
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public string DataType { get; private set; } = default!;

    private AuditFieldChange() { }

    public static AuditFieldChange Create(Guid auditLogId, string fieldName, string? oldValue, string? newValue, string dataType)
        => new()
        {
            AuditLogId = auditLogId,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            DataType = dataType
        };
}
