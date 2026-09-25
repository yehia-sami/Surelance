using Hangfire;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Features.Disputes;
using Surelance.Application.Features.Milestones;
using Surelance.Infrastructure.Outbox;

namespace Surelance.Infrastructure.Jobs;

public interface IRecurringJobsService
{
    Task CheckDisputeSlaAsync();
    Task CheckMilestoneDeadlinesAsync();
    void RegisterHangfireJobs();
}

public class RecurringJobsService : IRecurringJobsService
{
    public const int AutoApproveAfterDays = 7;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringJobsService> _logger;

    public RecurringJobsService(IServiceScopeFactory scopeFactory, ILogger<RecurringJobsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task CheckDisputeSlaAsync()
    {
        _logger.LogInformation("Hangfire Job: Checking dispute SLA deadlines...");
        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await mediator.Send(new CheckDisputeSlaCommand());
        if (result.IsSuccess && result.Value > 0)
        {
            _logger.LogInformation("Hangfire Job: Automatically resolved {Count} expired disputes based on default SLA rule.", result.Value);
        }
    }

    public async Task CheckMilestoneDeadlinesAsync()
    {
        _logger.LogInformation("Hangfire Job: Checking milestones for silence auto-release and approaching/past deadlines...");
        using var scope = _scopeFactory.CreateScope();
        var milestoneRepo = scope.ServiceProvider.GetRequiredService<IMilestoneRepository>();
        var escrowLedgerRepo = scope.ServiceProvider.GetRequiredService<IEscrowLedgerRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notifier = scope.ServiceProvider.GetRequiredService<IMilestoneNotifier>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var nowUtc = dateTimeProvider.UtcNow;

        // Auto-approve milestones submitted more than 7 days ago with no client action
        var silenceCutoffUtc = nowUtc.AddDays(-AutoApproveAfterDays);
        var silentMilestones = await milestoneRepo.GetSubmittedBeforeAsync(silenceCutoffUtc);
        foreach (var milestone in silentMilestones)
        {
            var clientId = milestone.Contract?.ClientId ?? Guid.Empty;
            var result = await ApproveMilestoneCommandHandler.ExecuteApproveCoreAsync(
                milestone,
                clientId,
                nowUtc,
                escrowLedgerRepo,
                unitOfWork,
                customDescription: "Auto-approved after 7 days of client inactivity following submission.",
                createdByUserId: null);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Hangfire Job: Auto-approved milestone {MilestoneId} ('{Title}') after {Days} days of client silence.",
                    milestone.Id, milestone.Title, AutoApproveAfterDays);
            }
            else
            {
                _logger.LogWarning("Hangfire Job: Failed to auto-approve milestone {MilestoneId}: {Error}",
                    milestone.Id, result.ErrorMessage);
            }
        }

        // Send reminders for funded milestones approaching or past deadline
        var pastDeadlineMilestones = await milestoneRepo.GetPendingFundedPastDeadlineAsync(nowUtc);
        foreach (var milestone in pastDeadlineMilestones)
        {
            await notifier.SendDeadlineReminderAsync(milestone.Id, milestone.Title);
        }
    }

    public void RegisterHangfireJobs()
    {
        RecurringJob.AddOrUpdate<IRecurringJobsService>(
            "dispute-sla-auto-resolution",
            service => service.CheckDisputeSlaAsync(),
            "*/2 * * * *");

        RecurringJob.AddOrUpdate<IRecurringJobsService>(
            "milestone-deadline-reminders",
            service => service.CheckMilestoneDeadlinesAsync(),
            "0 * * * *");

        RecurringJob.AddOrUpdate<IOutboxProcessor>(
            "outbox-dispatch",
            processor => processor.ProcessOutboxMessagesAsync(default),
            "*/1 * * * *");
    }
}
