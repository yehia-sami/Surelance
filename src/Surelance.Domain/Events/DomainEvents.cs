using Surelance.Domain.Common;
using Surelance.Domain.Enums;

namespace Surelance.Domain.Events;

public record MilestoneFundedEvent(
    Guid MilestoneId,
    Guid ContractId,
    decimal Amount,
    Guid ClientId,
    DateTime OccurredOnUtc) : IDomainEvent;

public record MilestoneSubmittedEvent(
    Guid MilestoneId,
    Guid ContractId,
    Guid FreelancerId,
    DateTime OccurredOnUtc) : IDomainEvent;

public record MilestoneApprovedEvent(
    Guid MilestoneId,
    Guid ContractId,
    decimal Amount,
    Guid ClientId,
    DateTime OccurredOnUtc) : IDomainEvent;

public record MilestoneDisputedEvent(
    Guid MilestoneId,
    Guid DisputeId,
    Guid RaisedByUserId,
    DateTime OccurredOnUtc) : IDomainEvent;

public record DisputeResolvedEvent(
    Guid DisputeId,
    Guid MilestoneId,
    DisputeResolution Resolution,
    Guid? ArbitratorId,
    DateTime OccurredOnUtc) : IDomainEvent;
