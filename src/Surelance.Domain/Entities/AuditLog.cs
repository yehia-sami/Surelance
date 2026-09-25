namespace Surelance.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public string? UserEmail { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityName { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;
    public bool Succeeded { get; private set; }
    public DateTime TimestampUtc { get; private set; }

    private AuditLog() { } // EF Core

    public AuditLog(
        Guid id,
        Guid? userId,
        string? userEmail,
        string action,
        string entityName,
        string entityId,
        string details,
        bool succeeded,
        DateTime timestampUtc)
    {
        Id = id;
        UserId = userId;
        UserEmail = userEmail;
        Action = action;
        EntityName = entityName;
        EntityId = entityId;
        Details = details;
        Succeeded = succeeded;
        TimestampUtc = timestampUtc;
    }
}
