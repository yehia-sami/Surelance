using Surelance.Domain.Common;
using Surelance.Domain.Enums;

namespace Surelance.Domain.Entities;

public class Contract : BaseEntity
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid ClientId { get; private set; }
    public User? Client { get; private set; }
    public Guid FreelancerId { get; private set; }
    public User? Freelancer { get; private set; }
    public ContractStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private readonly List<Milestone> _milestones = new();
    public IReadOnlyCollection<Milestone> Milestones => _milestones.AsReadOnly();

    private Contract() { } // EF Core

    public Contract(Guid id, string title, string description, Guid clientId, Guid freelancerId, DateTime createdAtUtc)
    {
        Id = id;
        Title = title.Trim();
        Description = description.Trim();
        ClientId = clientId;
        FreelancerId = freelancerId;
        Status = ContractStatus.Draft;
        CreatedAtUtc = createdAtUtc;
    }

    public void Accept()
    {
        if (Status != ContractStatus.Draft)
        {
            throw new InvalidOperationException($"Contract in status {Status} cannot be accepted.");
        }
        Status = ContractStatus.Active;
    }

    public void Cancel()
    {
        if (Status == ContractStatus.Completed)
        {
            throw new InvalidOperationException("Completed contracts cannot be cancelled.");
        }
        Status = ContractStatus.Cancelled;
    }

    public void Close()
    {
        if (Status != ContractStatus.Active)
        {
            throw new InvalidOperationException($"Contract in status '{Status}' cannot be closed.");
        }

        if (_milestones.Count == 0)
        {
            throw new InvalidOperationException("Cannot close a contract with no milestones.");
        }

        bool allSettled = _milestones.All(m =>
            m.Status is MilestoneStatus.Released or MilestoneStatus.Refunded);

        if (!allSettled)
        {
            throw new InvalidOperationException("Cannot close contract while there are unsettled milestones.");
        }

        Status = ContractStatus.Completed;
    }

    public void AddMilestone(Milestone milestone)
    {
        _milestones.Add(milestone);
    }
}
