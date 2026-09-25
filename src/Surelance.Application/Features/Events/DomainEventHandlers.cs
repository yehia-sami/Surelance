using MediatR;
using Microsoft.Extensions.Logging;
using Surelance.Application.Common.Interfaces;
using Surelance.Domain.Common;
using Surelance.Domain.Entities;
using Surelance.Domain.Events;
using System.Text.Json;

namespace Surelance.Application.Features.Events;

public record DomainEventNotification<TEvent>(TEvent DomainEvent) : INotification 
    where TEvent : IDomainEvent;

public class MilestoneFundedNotificationHandler : INotificationHandler<DomainEventNotification<MilestoneFundedEvent>>
{
    private readonly IMilestoneNotifier _notifier;
    private readonly ILogger<MilestoneFundedNotificationHandler> _logger;

    public MilestoneFundedNotificationHandler(IMilestoneNotifier notifier, ILogger<MilestoneFundedNotificationHandler> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<MilestoneFundedEvent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        _logger.LogInformation("DomainEvent: Milestone {MilestoneId} funded with amount {Amount:C} by Client {ClientId}",
            e.MilestoneId, e.Amount, e.ClientId);

        await _notifier.NotifyMilestoneFundedAsync(e.MilestoneId, e.ContractId, e.Amount, cancellationToken);
    }
}

public class MilestoneSubmittedNotificationHandler : INotificationHandler<DomainEventNotification<MilestoneSubmittedEvent>>
{
    private readonly IMilestoneNotifier _notifier;
    private readonly ILogger<MilestoneSubmittedNotificationHandler> _logger;

    public MilestoneSubmittedNotificationHandler(IMilestoneNotifier notifier, ILogger<MilestoneSubmittedNotificationHandler> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<MilestoneSubmittedEvent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        _logger.LogInformation("DomainEvent: Milestone {MilestoneId} submitted for review by Freelancer {FreelancerId}",
            e.MilestoneId, e.FreelancerId);

        await _notifier.NotifyMilestoneSubmittedAsync(e.MilestoneId, e.ContractId, cancellationToken);
    }
}

public class MilestoneApprovedNotificationHandler : INotificationHandler<DomainEventNotification<MilestoneApprovedEvent>>
{
    private readonly IMilestoneNotifier _notifier;
    private readonly ILogger<MilestoneApprovedNotificationHandler> _logger;

    public MilestoneApprovedNotificationHandler(IMilestoneNotifier notifier, ILogger<MilestoneApprovedNotificationHandler> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<MilestoneApprovedEvent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        _logger.LogInformation("DomainEvent: Milestone {MilestoneId} approved by Client {ClientId}, released {Amount:C}",
            e.MilestoneId, e.ClientId, e.Amount);

        await _notifier.NotifyMilestoneApprovedAsync(e.MilestoneId, e.ContractId, e.Amount, cancellationToken);
    }
}

public class MilestoneDisputedNotificationHandler : INotificationHandler<DomainEventNotification<MilestoneDisputedEvent>>
{
    private readonly IMilestoneNotifier _notifier;
    private readonly ILogger<MilestoneDisputedNotificationHandler> _logger;

    public MilestoneDisputedNotificationHandler(IMilestoneNotifier notifier, ILogger<MilestoneDisputedNotificationHandler> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<MilestoneDisputedEvent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        _logger.LogWarning("DomainEvent: Dispute {DisputeId} raised on Milestone {MilestoneId} by User {UserId}",
            e.DisputeId, e.MilestoneId, e.RaisedByUserId);

        await _notifier.NotifyDisputeRaisedAsync(e.MilestoneId, e.DisputeId, cancellationToken);
    }
}

public class DisputeResolvedNotificationHandler : INotificationHandler<DomainEventNotification<DisputeResolvedEvent>>
{
    private readonly IMilestoneNotifier _notifier;
    private readonly ILogger<DisputeResolvedNotificationHandler> _logger;

    public DisputeResolvedNotificationHandler(IMilestoneNotifier notifier, ILogger<DisputeResolvedNotificationHandler> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<DisputeResolvedEvent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        _logger.LogInformation("DomainEvent: Dispute {DisputeId} on Milestone {MilestoneId} resolved with {Resolution} by Arbitrator {ArbitratorId}",
            e.DisputeId, e.MilestoneId, e.Resolution, e.ArbitratorId);

        await _notifier.NotifyDisputeResolvedAsync(e.DisputeId, e.MilestoneId, e.Resolution, cancellationToken);
    }
}
