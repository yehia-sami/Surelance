using Surelance.Domain.Common;
using Surelance.Domain.Enums;
using Surelance.Domain.Events;

namespace Surelance.Domain.Entities;

public class Milestone : BaseEntity
{
    public Guid Id { get; private set; }
    public Guid ContractId { get; private set; }
    public Contract? Contract { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public MilestoneStatus Status { get; private set; }
    public DateTime? DueDateUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? FundedAtUtc { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? ReleasedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private readonly List<EscrowLedgerEntry> _ledgerEntries = new();
    public IReadOnlyCollection<EscrowLedgerEntry> LedgerEntries => _ledgerEntries.AsReadOnly();

    private readonly List<Dispute> _disputes = new();
    public IReadOnlyCollection<Dispute> Disputes => _disputes.AsReadOnly();

    private Milestone() { } // EF Core

    public Milestone(Guid id, Guid contractId, string title, string description, decimal amount, DateTime? dueDateUtc, DateTime createdAtUtc)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Milestone amount must be greater than zero.", nameof(amount));
        }

        Id = id;
        ContractId = contractId;
        Title = title.Trim();
        Description = description.Trim();
        Amount = amount;
        Status = MilestoneStatus.Pending;
        DueDateUtc = dueDateUtc;
        CreatedAtUtc = createdAtUtc;
    }

    internal void SetContract(Contract contract)
    {
        Contract = contract ?? throw new ArgumentNullException(nameof(contract));
        ContractId = contract.Id;
    }

    public void Fund(Guid clientId, DateTime nowUtc)
    {
        EnsureNotLocked();

        if (Status != MilestoneStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot fund milestone in status '{Status}'. It must be 'Pending'.");
        }

        Status = MilestoneStatus.Funded;
        FundedAtUtc = nowUtc;

        AddDomainEvent(new MilestoneFundedEvent(Id, ContractId, Amount, clientId, nowUtc));
    }

    public void Submit(Guid freelancerId, DateTime nowUtc)
    {
        EnsureNotLocked();

        if (Status != MilestoneStatus.Funded)
        {
            throw new InvalidOperationException($"Cannot submit milestone in status '{Status}'. Milestone must be 'Funded' first.");
        }

        Status = MilestoneStatus.Submitted;
        SubmittedAtUtc = nowUtc;

        AddDomainEvent(new MilestoneSubmittedEvent(Id, ContractId, freelancerId, nowUtc));
    }

    public void Approve(Guid clientId, DateTime nowUtc)
    {
        EnsureNotLocked();

        if (Status != MilestoneStatus.Submitted)
        {
            throw new InvalidOperationException($"Cannot approve milestone in status '{Status}'. Milestone must be 'Submitted'.");
        }

        Status = MilestoneStatus.Released;
        ReleasedAtUtc = nowUtc;

        AddDomainEvent(new MilestoneApprovedEvent(Id, ContractId, Amount, clientId, nowUtc));
    }

    public void MarkDisputed(Guid disputeId, Guid raisedByUserId, DateTime nowUtc)
    {
        EnsureNotLocked();

        if (Status != MilestoneStatus.Submitted)
        {
            throw new InvalidOperationException($"Cannot dispute milestone in status '{Status}'. Only 'Submitted' milestones can be disputed.");
        }

        Status = MilestoneStatus.Disputed;

        AddDomainEvent(new MilestoneDisputedEvent(Id, disputeId, raisedByUserId, nowUtc));
    }

    public void Resolve(DisputeResolution resolution, DateTime nowUtc)
    {
        if (Status != MilestoneStatus.Disputed)
        {
            throw new InvalidOperationException($"Cannot resolve milestone in status '{Status}'. Milestone must be 'Disputed'.");
        }

        Status = resolution switch
        {
            DisputeResolution.ReleaseFreelancer => MilestoneStatus.Released,
            DisputeResolution.RefundClient => MilestoneStatus.Refunded,
            _ => throw new ArgumentException("A valid resolution (RefundClient or ReleaseFreelancer) must be specified.", nameof(resolution))
        };

        if (Status == MilestoneStatus.Released)
        {
            ReleasedAtUtc = nowUtc;
        }
    }

    private void EnsureNotLocked()
    {
        if (Status is MilestoneStatus.Released or MilestoneStatus.Refunded)
        {
            throw new InvalidOperationException($"Milestone {Id} is locked in status '{Status}' and cannot be modified.");
        }
    }
}
