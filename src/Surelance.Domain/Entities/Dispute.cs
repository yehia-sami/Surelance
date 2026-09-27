using Surelance.Domain.Common;
using Surelance.Domain.Enums;
using Surelance.Domain.Events;

namespace Surelance.Domain.Entities;

public class Dispute : BaseEntity
{
    public Guid Id { get; private set; }
    public Guid MilestoneId { get; private set; }
    public Milestone? Milestone { get; private set; }
    public Guid RaisedByUserId { get; private set; }
    public User? RaisedByUser { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DisputeStatus Status { get; private set; }
    public DisputeResolution Resolution { get; private set; }
    public DateTime SlaExpiresAtUtc { get; private set; }
    public Guid? ArbitratorId { get; private set; }
    public User? Arbitrator { get; private set; }
    public string? ResolutionNotes { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Dispute() { } // EF Core

    public Dispute(
        Guid id,
        Guid milestoneId,
        Guid raisedByUserId,
        string reason,
        DateTime slaExpiresAtUtc,
        DateTime createdAtUtc,
        Guid? arbitratorId = null)
    {
        Id = id;
        MilestoneId = milestoneId;
        RaisedByUserId = raisedByUserId;
        Reason = reason.Trim();
        Status = DisputeStatus.Open;
        Resolution = DisputeResolution.None;
        SlaExpiresAtUtc = slaExpiresAtUtc;
        ArbitratorId = arbitratorId;
        CreatedAtUtc = createdAtUtc;
    }

    internal void SetMilestone(Milestone milestone)
    {
        Milestone = milestone ?? throw new ArgumentNullException(nameof(milestone));
        MilestoneId = milestone.Id;
    }

    public void Resolve(DisputeResolution resolution, string notes, Guid? arbitratorId, DateTime nowUtc)
    {
        if (Status != DisputeStatus.Open)
        {
            throw new InvalidOperationException($"Dispute {Id} is already resolved.");
        }

        if (resolution == DisputeResolution.None)
        {
            throw new ArgumentException("A concrete resolution (RefundClient or ReleaseFreelancer) must be chosen.", nameof(resolution));
        }

        Status = DisputeStatus.Resolved;
        Resolution = resolution;
        ResolutionNotes = notes.Trim();
        ArbitratorId = arbitratorId ?? ArbitratorId;
        ResolvedAtUtc = nowUtc;

        AddDomainEvent(new DisputeResolvedEvent(Id, MilestoneId, resolution, ArbitratorId, nowUtc));
    }

    public void ExtendSla(DateTime newDeadlineUtc)
    {
        if (Status != DisputeStatus.Open)
        {
            throw new InvalidOperationException($"Cannot extend SLA for dispute {Id} because it is in status '{Status}'. Only 'Open' disputes can have their SLA extended.");
        }

        if (newDeadlineUtc <= SlaExpiresAtUtc)
        {
            throw new InvalidOperationException($"New SLA deadline '{newDeadlineUtc:u}' must be later than current deadline '{SlaExpiresAtUtc:u}'.");
        }

        SlaExpiresAtUtc = newDeadlineUtc;
    }
}
