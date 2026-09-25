using Surelance.Domain.Common;
using Surelance.Domain.Enums;

namespace Surelance.Domain.Entities;

public class EscrowLedgerEntry : BaseEntity
{
    public Guid Id { get; private set; }
    public Guid MilestoneId { get; private set; }
    public Milestone? Milestone { get; private set; }
    public decimal Amount { get; private set; }
    public LedgerEntryType EntryType { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    private EscrowLedgerEntry() { } // EF Core

    public EscrowLedgerEntry(
        Guid id,
        Guid milestoneId,
        decimal amount,
        LedgerEntryType entryType,
        string description,
        DateTime createdAtUtc,
        Guid? createdByUserId)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Ledger entry amount must be greater than zero.", nameof(amount));
        }

        Id = id;
        MilestoneId = milestoneId;
        Amount = amount;
        EntryType = entryType;
        Description = description.Trim();
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
    }
}
