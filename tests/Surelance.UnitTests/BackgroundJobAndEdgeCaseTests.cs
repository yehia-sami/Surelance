using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Application.Features.Contracts;
using Surelance.Application.Features.Disputes;
using Surelance.Application.Features.Milestones;
using Surelance.Domain.Common;
using Surelance.Domain.Entities;
using Surelance.Domain.Enums;
using Surelance.Domain.Events;
using Surelance.Infrastructure.Jobs;
using Surelance.Infrastructure.Outbox;
using Surelance.Infrastructure.Persistence;
using Surelance.Infrastructure.Persistence.Repositories;
using System.Text.Json;
using Xunit;

namespace Surelance.UnitTests;

public class BackgroundJobAndEdgeCaseTests
{
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _clientId = Guid.NewGuid();
    private readonly Guid _freelancerId = Guid.NewGuid();
    private readonly Guid _arbitratorId = Guid.NewGuid();

    [Fact]
    public async Task HangfireRecurringJob_CheckDisputeSla_CallsMediatorAndLogsSuccess()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        var mediatorMock = new Mock<ISender>();
        var loggerMock = new Mock<ILogger<RecurringJobsService>>();

        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(scopeMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ISender))).Returns(mediatorMock.Object);

        mediatorMock.Setup(m => m.Send(It.IsAny<CheckDisputeSlaCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(2));

        var jobService = new RecurringJobsService(scopeFactoryMock.Object, loggerMock.Object);

        await jobService.CheckDisputeSlaAsync();

        mediatorMock.Verify(m => m.Send(It.IsAny<CheckDisputeSlaCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HangfireRecurringJob_CheckMilestoneDeadlines_AutoApprovesSilentMilestonesSubmittedOver7DaysAgo()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        var milestoneRepoMock = new Mock<IMilestoneRepository>();
        var escrowLedgerRepoMock = new Mock<IEscrowLedgerRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var notifierMock = new Mock<IMilestoneNotifier>();
        var dateTimeMock = new Mock<IDateTimeProvider>();
        var loggerMock = new Mock<ILogger<RecurringJobsService>>();

        dateTimeMock.Setup(d => d.UtcNow).Returns(_now);

        var contract = new Contract(Guid.NewGuid(), "Contract", "Desc", _clientId, _freelancerId, _now.AddDays(-20));
        contract.Accept();
        var silentMilestone = new Milestone(Guid.NewGuid(), contract.Id, "Silent Milestone", "Desc", 1200m, _now.AddDays(5), _now.AddDays(-20));
        silentMilestone.Fund(_clientId, _now.AddDays(-15));
        silentMilestone.Submit(_freelancerId, _now.AddDays(-10)); // Submitted 10 days ago ( > 7 days)
        silentMilestone.SetContract(contract);

        milestoneRepoMock.Setup(m => m.GetSubmittedBeforeAsync(_now.AddDays(-RecurringJobsService.AutoApproveAfterDays), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Milestone> { silentMilestone });

        milestoneRepoMock.Setup(m => m.GetPendingFundedPastDeadlineAsync(_now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Milestone>());

        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(scopeMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IMilestoneRepository))).Returns(milestoneRepoMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IEscrowLedgerRepository))).Returns(escrowLedgerRepoMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IUnitOfWork))).Returns(unitOfWorkMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IMilestoneNotifier))).Returns(notifierMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IDateTimeProvider))).Returns(dateTimeMock.Object);

        var jobService = new RecurringJobsService(scopeFactoryMock.Object, loggerMock.Object);

        await jobService.CheckMilestoneDeadlinesAsync();

        silentMilestone.Status.Should().Be(MilestoneStatus.Released);
        silentMilestone.ReleasedAtUtc.Should().Be(_now);

        escrowLedgerRepoMock.Verify(l => l.AddAsync(
            It.Is<EscrowLedgerEntry>(e =>
                e.MilestoneId == silentMilestone.Id &&
                e.Amount == 1200m &&
                e.EntryType == LedgerEntryType.Release &&
                e.CreatedByUserId == null &&
                e.Description.Contains("Auto-approved after 7 days of client inactivity following submission.")),
            It.IsAny<CancellationToken>()), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OutboxProcessor_DispatchesMessagesAndMarksThemProcessed()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        var outboxRepoMock = new Mock<IOutboxRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var publisherMock = new Mock<IPublisher>();
        var dateTimeMock = new Mock<IDateTimeProvider>();
        var loggerMock = new Mock<ILogger<OutboxProcessor>>();

        dateTimeMock.Setup(d => d.UtcNow).Returns(_now);

        var domainEvent = new MilestoneApprovedEvent(Guid.NewGuid(), Guid.NewGuid(), 1000m, _clientId, _now);
        var message = new OutboxMessage(
            Guid.NewGuid(),
            _now,
            typeof(MilestoneApprovedEvent).AssemblyQualifiedName!,
            JsonSerializer.Serialize(domainEvent));

        outboxRepoMock.Setup(r => r.GetUnprocessedMessagesAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { message });

        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(scopeMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IOutboxRepository))).Returns(outboxRepoMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IUnitOfWork))).Returns(unitOfWorkMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IPublisher))).Returns(publisherMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IDateTimeProvider))).Returns(dateTimeMock.Object);

        var processor = new OutboxProcessor(scopeFactoryMock.Object, loggerMock.Object);

        await processor.ProcessOutboxMessagesAsync(TestContext.Current.CancellationToken);

        message.ProcessedOnUtc.Should().Be(_now);
        message.Error.Should().BeNull();
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OutboxProcessor_WhenMessageInvalid_MarksFailedGracefullyWithoutCrashing()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        var outboxRepoMock = new Mock<IOutboxRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var publisherMock = new Mock<IPublisher>();
        var dateTimeMock = new Mock<IDateTimeProvider>();
        var loggerMock = new Mock<ILogger<OutboxProcessor>>();

        dateTimeMock.Setup(d => d.UtcNow).Returns(_now);

        var corruptedMessage = new OutboxMessage(
            Guid.NewGuid(),
            _now,
            typeof(MilestoneApprovedEvent).AssemblyQualifiedName!,
            "INVALID_CORRUPTED_JSON");

        outboxRepoMock.Setup(r => r.GetUnprocessedMessagesAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { corruptedMessage });

        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(scopeMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IOutboxRepository))).Returns(outboxRepoMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IUnitOfWork))).Returns(unitOfWorkMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IPublisher))).Returns(publisherMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IDateTimeProvider))).Returns(dateTimeMock.Object);

        var processor = new OutboxProcessor(scopeFactoryMock.Object, loggerMock.Object);

        await processor.ProcessOutboxMessagesAsync(TestContext.Current.CancellationToken);

        corruptedMessage.Error.Should().NotBeNullOrEmpty();
        corruptedMessage.ProcessedOnUtc.Should().BeNull();
        corruptedMessage.RetryCount.Should().Be(1);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OutboxMessage_MarkFailed_LeavesProcessedOnUtcNullAndIncrementsRetryCount()
    {
        var msg = new OutboxMessage(Guid.NewGuid(), _now, "SomeType", "{}");
        msg.RetryCount.Should().Be(0);
        msg.ProcessedOnUtc.Should().BeNull();
        msg.LastFailedAtUtc.Should().BeNull();

        msg.MarkFailed(_now, "Network error");

        msg.ProcessedOnUtc.Should().BeNull();
        msg.Error.Should().Be("Network error");
        msg.RetryCount.Should().Be(1);
        msg.LastFailedAtUtc.Should().Be(_now);

        msg.MarkFailed(_now.AddMinutes(5), "Timeout error");
        msg.RetryCount.Should().Be(2);
        msg.ProcessedOnUtc.Should().BeNull();
        msg.LastFailedAtUtc.Should().Be(_now.AddMinutes(5));
    }

    [Fact]
    public void OutboxMessage_MarkProcessed_ClearsErrorAndSetsProcessedOnUtc()
    {
        var msg = new OutboxMessage(Guid.NewGuid(), _now, "SomeType", "{}");
        msg.MarkFailed(_now, "Network error");

        msg.MarkProcessed(_now);

        msg.ProcessedOnUtc.Should().Be(_now);
        msg.Error.Should().BeNull();
        msg.RetryCount.Should().Be(1);
    }

    [Fact]
    public async Task OutboxProcessor_FailedMessage_RemainsUnprocessedAndCanBeRetriedSuccessfully()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        var outboxRepoMock = new Mock<IOutboxRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var publisherMock = new Mock<IPublisher>();
        var dateTimeMock = new Mock<IDateTimeProvider>();
        var loggerMock = new Mock<ILogger<OutboxProcessor>>();

        dateTimeMock.Setup(d => d.UtcNow).Returns(_now);

        var domainEvent = new MilestoneApprovedEvent(Guid.NewGuid(), Guid.NewGuid(), 1000m, _clientId, _now);
        var message = new OutboxMessage(
            Guid.NewGuid(),
            _now,
            typeof(MilestoneApprovedEvent).AssemblyQualifiedName!,
            JsonSerializer.Serialize(domainEvent));

        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(scopeMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IOutboxRepository))).Returns(outboxRepoMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IUnitOfWork))).Returns(unitOfWorkMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IPublisher))).Returns(publisherMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IDateTimeProvider))).Returns(dateTimeMock.Object);

        // Attempt 1: Publisher throws transient exception
        publisherMock.Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Transient network blip"));

        outboxRepoMock.Setup(r => r.GetUnprocessedMessagesAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { message });

        var processor = new OutboxProcessor(scopeFactoryMock.Object, loggerMock.Object);

        await processor.ProcessOutboxMessagesAsync(TestContext.Current.CancellationToken);

        // Message failed: ProcessedOnUtc is NULL so it will be queried again, RetryCount incremented
        message.ProcessedOnUtc.Should().BeNull();
        message.Error.Should().Be("Transient network blip");
        message.RetryCount.Should().Be(1);
        message.LastFailedAtUtc.Should().Be(_now);

        // Attempt 2: Publisher recovers
        publisherMock.Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await processor.ProcessOutboxMessagesAsync(TestContext.Current.CancellationToken);

        // Message now succeeded: ProcessedOnUtc set, Error cleared
        message.ProcessedOnUtc.Should().Be(_now);
        message.Error.Should().BeNull();
        message.RetryCount.Should().Be(1);
    }

    [Fact]
    public void Contract_Cancel_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        var contract = new Contract(Guid.NewGuid(), "Title", "Desc", _clientId, _freelancerId, _now);
        contract.Accept();
        var m = new Milestone(Guid.NewGuid(), contract.Id, "M1", "D", 1000m, _now.AddDays(5), _now);
        m.Fund(_clientId, _now);
        m.Submit(_freelancerId, _now);
        m.Approve(_clientId, _now);
        contract.AddMilestone(m);
        contract.CompleteIfAllMilestonesSettled();

        contract.Status.Should().Be(ContractStatus.Completed);

        var act = () => contract.Cancel();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Completed contracts cannot be cancelled*");
    }

    [Fact]
    public void Dispute_Resolve_WithNoneResolution_ThrowsArgumentException()
    {
        var dispute = new Dispute(Guid.NewGuid(), Guid.NewGuid(), _clientId, "Reason", _now.AddDays(5), _now);

        var act = () => dispute.Resolve(DisputeResolution.None, "Notes", _arbitratorId, _now);
        act.Should().Throw<ArgumentException>().WithMessage("*concrete resolution*");
    }

    [Fact]
    public async Task OutboxProcessor_WhenMessageHitsMaxRetries_LogsWarning()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        var outboxRepoMock = new Mock<IOutboxRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var publisherMock = new Mock<IPublisher>();
        var dateTimeMock = new Mock<IDateTimeProvider>();
        var loggerMock = new Mock<ILogger<OutboxProcessor>>();

        dateTimeMock.Setup(d => d.UtcNow).Returns(_now);

        var message = new OutboxMessage(
            Guid.NewGuid(),
            _now,
            "NonExistent.Type",
            "{}");

        // Fail 9 times beforehand
        for (int i = 0; i < 9; i++)
        {
            message.MarkFailed(_now.AddMinutes(-20), "Previous error");
        }
        message.RetryCount.Should().Be(9);

        outboxRepoMock.Setup(r => r.GetUnprocessedMessagesAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { message });

        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(scopeMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IOutboxRepository))).Returns(outboxRepoMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IUnitOfWork))).Returns(unitOfWorkMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IPublisher))).Returns(publisherMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IDateTimeProvider))).Returns(dateTimeMock.Object);

        var processor = new OutboxProcessor(scopeFactoryMock.Object, loggerMock.Object);

        await processor.ProcessOutboxMessagesAsync(TestContext.Current.CancellationToken);

        message.RetryCount.Should().Be(10);
        message.LastFailedAtUtc.Should().Be(_now);

        // Verify Warning log was emitted
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("maximum retry count")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUnprocessedMessagesAsync_WhenMessageJustFailed_IsNotReturnedWithinBackoffWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(ct);

        try
        {
            var options = new DbContextOptionsBuilder<SurelanceDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var initContext = new SurelanceDbContext(options))
            {
                await initContext.Database.EnsureCreatedAsync(ct);

                var message = new OutboxMessage(Guid.NewGuid(), _now, "TestType", "{}");
                // Failed 1 minute ago, RetryCount = 1 -> backoff = 1 * 2 = 2 minutes. Still within backoff at _now.
                message.MarkFailed(_now.AddMinutes(-1), "Failure 1");

                await initContext.OutboxMessages.AddAsync(message, ct);
                await initContext.SaveChangesAsync(ct);
            }

            using (var queryContext = new SurelanceDbContext(options))
            {
                var repo = new OutboxRepository(queryContext);
                var messages = await repo.GetUnprocessedMessagesAsync(_now, cancellationToken: ct);

                messages.Should().BeEmpty();
            }
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
