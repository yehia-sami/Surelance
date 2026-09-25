namespace Surelance.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTime? ProcessedOnUtc { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime? LastFailedAtUtc { get; private set; }

    private OutboxMessage() { } // EF Core

    public OutboxMessage(Guid id, DateTime occurredOnUtc, string type, string content)
    {
        Id = id;
        OccurredOnUtc = occurredOnUtc;
        Type = type;
        Content = content;
        RetryCount = 0;
    }

    public void MarkProcessed(DateTime processedOnUtc)
    {
        ProcessedOnUtc = processedOnUtc;
        Error = null;
    }

    public void MarkFailed(DateTime nowUtc, string error)
    {
        // ProcessedOnUtc is left null so the message is eligible for retry
        Error = error;
        RetryCount++;
        LastFailedAtUtc = nowUtc;
    }
}
