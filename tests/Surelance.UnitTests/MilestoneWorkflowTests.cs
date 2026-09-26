using FluentAssertions;
using Moq;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Application.Features.Contracts;
using Surelance.Application.Features.Milestones;
using Surelance.Domain.Entities;
using Surelance.Domain.Enums;
using Surelance.Domain.Events;
using Xunit;

namespace Surelance.UnitTests;

public class MilestoneWorkflowTests
{
    private readonly Mock<IMilestoneRepository> _milestoneRepoMock;
    private readonly Mock<IEscrowLedgerRepository> _ledgerRepoMock;
    private readonly Mock<IContractRepository> _contractRepoMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IDateTimeProvider> _dateTimeMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    private readonly Guid _clientId = Guid.NewGuid();
    private readonly Guid _freelancerId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public MilestoneWorkflowTests()
    {
        _milestoneRepoMock = new Mock<IMilestoneRepository>();
        _ledgerRepoMock = new Mock<IEscrowLedgerRepository>();
        _contractRepoMock = new Mock<IContractRepository>();
        _currentUserMock = new Mock<ICurrentUserService>();
        _dateTimeMock = new Mock<IDateTimeProvider>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _dateTimeMock.Setup(d => d.UtcNow).Returns(_now);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private (Contract contract, Milestone milestone) CreateTestContractAndMilestone(MilestoneStatus status)
    {
        var contract = new Contract(Guid.NewGuid(), "Test Contract", "Desc", _clientId, _freelancerId, _now);
        contract.Accept();

        var milestone = new Milestone(Guid.NewGuid(), contract.Id, "Milestone 1", "Desc", 1000m, _now.AddDays(7), _now);

        if (status >= MilestoneStatus.Funded)
        {
            milestone.Fund(_clientId, _now);
        }

        if (status >= MilestoneStatus.Submitted)
        {
            milestone.Submit(_freelancerId, _now);
        }

        if (status == MilestoneStatus.Released)
        {
            milestone.Approve(_clientId, _now);
        }

        milestone.ClearDomainEvents();

        milestone.SetContract(contract);
        contract.AddMilestone(milestone);

        return (contract, milestone);
    }

    [Fact]
    public async Task FundMilestone_WhenClientCallsOnPendingMilestone_ShouldSucceedAndCreateLedgerFundEntry()
    {
        var (_, milestone) = CreateTestContractAndMilestone(MilestoneStatus.Pending);
        _currentUserMock.Setup(u => u.UserId).Returns(_clientId);
        _currentUserMock.Setup(u => u.Role).Returns(UserRole.Client);

        _milestoneRepoMock.Setup(r => r.GetByIdWithContractAsync(milestone.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(milestone);

        var handler = new FundMilestoneCommandHandler(
            _milestoneRepoMock.Object,
            _ledgerRepoMock.Object,
            _currentUserMock.Object,
            _dateTimeMock.Object,
            _unitOfWorkMock.Object);

        var result = await handler.Handle(new FundMilestoneCommand(milestone.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        milestone.Status.Should().Be(MilestoneStatus.Funded);

        _ledgerRepoMock.Verify(l => l.AddAsync(
            It.Is<EscrowLedgerEntry>(e => 
                e.MilestoneId == milestone.Id && 
                e.Amount == 1000m && 
                e.EntryType == LedgerEntryType.Fund && 
                e.CreatedByUserId == _clientId), 
            It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        milestone.DomainEvents.Should().ContainSingle(e => e is MilestoneFundedEvent);
    }

    [Fact]
    public async Task FundMilestone_WhenContractNotYetAccepted_ShouldFailWithConflict()
    {
        var draftContract = new Contract(Guid.NewGuid(), "Draft Contract", "Desc", _clientId, _freelancerId, _now);
        var milestone = new Milestone(Guid.NewGuid(), draftContract.Id, "Milestone 1", "Desc", 1000m, _now.AddDays(7), _now);
        milestone.SetContract(draftContract);

        _currentUserMock.Setup(u => u.UserId).Returns(_clientId);
        _milestoneRepoMock.Setup(r => r.GetByIdWithContractAsync(milestone.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(milestone);

        var handler = new FundMilestoneCommandHandler(
            _milestoneRepoMock.Object,
            _ledgerRepoMock.Object,
            _currentUserMock.Object,
            _dateTimeMock.Object,
            _unitOfWorkMock.Object);

        var result = await handler.Handle(new FundMilestoneCommand(milestone.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
        milestone.Status.Should().Be(MilestoneStatus.Pending);
        _ledgerRepoMock.Verify(l => l.AddAsync(It.IsAny<EscrowLedgerEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FundMilestone_WhenNonClientCalls_ShouldFail()
    {
        var (_, milestone) = CreateTestContractAndMilestone(MilestoneStatus.Pending);
        _currentUserMock.Setup(u => u.UserId).Returns(_freelancerId);

        _milestoneRepoMock.Setup(r => r.GetByIdWithContractAsync(milestone.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(milestone);

        var handler = new FundMilestoneCommandHandler(
            _milestoneRepoMock.Object,
            _ledgerRepoMock.Object,
            _currentUserMock.Object,
            _dateTimeMock.Object,
            _unitOfWorkMock.Object);

        var result = await handler.Handle(new FundMilestoneCommand(milestone.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Only the client");
        milestone.Status.Should().Be(MilestoneStatus.Pending);
    }

    [Fact]
    public async Task SubmitMilestone_WhenFundedAndCalledByFreelancer_ShouldSucceed()
    {
        var (_, milestone) = CreateTestContractAndMilestone(MilestoneStatus.Funded);
        _currentUserMock.Setup(u => u.UserId).Returns(_freelancerId);
        _currentUserMock.Setup(u => u.Role).Returns(UserRole.Freelancer);

        _milestoneRepoMock.Setup(r => r.GetByIdWithContractAsync(milestone.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(milestone);

        var handler = new SubmitMilestoneCommandHandler(
            _milestoneRepoMock.Object,
            _currentUserMock.Object,
            _dateTimeMock.Object,
            _unitOfWorkMock.Object);

        var result = await handler.Handle(new SubmitMilestoneCommand(milestone.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        milestone.Status.Should().Be(MilestoneStatus.Submitted);
        milestone.DomainEvents.Should().ContainSingle(e => e is MilestoneSubmittedEvent);
    }

    [Fact]
    public async Task ApproveMilestone_WhenSubmittedAndCalledByClient_ShouldReleaseFundsAndCreateLedgerEntry()
    {
        var (contract, milestone) = CreateTestContractAndMilestone(MilestoneStatus.Submitted);
        _currentUserMock.Setup(u => u.UserId).Returns(_clientId);
        _currentUserMock.Setup(u => u.Role).Returns(UserRole.Client);

        _milestoneRepoMock.Setup(r => r.GetByIdWithContractAsync(milestone.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(milestone);
        _contractRepoMock.Setup(c => c.GetByIdWithMilestonesAsync(contract.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contract);

        var handler = new ApproveMilestoneCommandHandler(
            _milestoneRepoMock.Object,
            _ledgerRepoMock.Object,
            _currentUserMock.Object,
            _dateTimeMock.Object,
            _unitOfWorkMock.Object);

        var result = await handler.Handle(new ApproveMilestoneCommand(milestone.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        milestone.Status.Should().Be(MilestoneStatus.Released);

        _ledgerRepoMock.Verify(l => l.AddAsync(
            It.Is<EscrowLedgerEntry>(e => 
                e.MilestoneId == milestone.Id && 
                e.Amount == 1000m && 
                e.EntryType == LedgerEntryType.Release && 
                e.CreatedByUserId == _clientId), 
            It.IsAny<CancellationToken>()), Times.Once);

        milestone.DomainEvents.Should().ContainSingle(e => e is MilestoneApprovedEvent);
        contract.Status.Should().Be(ContractStatus.Active);
    }

    [Fact]
    public async Task ApproveMilestone_WhenNotSubmitted_ShouldFail()
    {
        var (_, milestone) = CreateTestContractAndMilestone(MilestoneStatus.Funded);
        _currentUserMock.Setup(u => u.UserId).Returns(_clientId);

        _milestoneRepoMock.Setup(r => r.GetByIdWithContractAsync(milestone.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(milestone);

        var handler = new ApproveMilestoneCommandHandler(
            _milestoneRepoMock.Object,
            _ledgerRepoMock.Object,
            _currentUserMock.Object,
            _dateTimeMock.Object,
            _unitOfWorkMock.Object);

        var result = await handler.Handle(new ApproveMilestoneCommand(milestone.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Submitted");
    }

    [Fact]
    public void LockedMilestone_CannotBeModifiedAfterReleased()
    {
        var (_, milestone) = CreateTestContractAndMilestone(MilestoneStatus.Released);

        var actFund = () => milestone.Fund(_clientId, _now);
        actFund.Should().Throw<InvalidOperationException>().WithMessage("*locked*");

        var actSubmit = () => milestone.Submit(_freelancerId, _now);
        actSubmit.Should().Throw<InvalidOperationException>().WithMessage("*locked*");

        var actApprove = () => milestone.Approve(_clientId, _now);
        actApprove.Should().Throw<InvalidOperationException>().WithMessage("*locked*");
    }

    [Fact]
    public async Task CloseContract_WhenAllMilestonesSettled_ShouldSucceedAndMarkCompleted()
    {
        var (contract, milestone1) = CreateTestContractAndMilestone(MilestoneStatus.Released);
        contract.AddMilestone(milestone1);

        _currentUserMock.Setup(u => u.UserId).Returns(_clientId);
        _contractRepoMock.Setup(c => c.GetByIdWithMilestonesAsync(contract.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contract);

        var handler = new CloseContractCommandHandler(
            _contractRepoMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        var result = await handler.Handle(new CloseContractCommand(contract.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        contract.Status.Should().Be(ContractStatus.Completed);
    }

    [Fact]
    public async Task CloseContract_WhenMilestonesAreStillUnsettled_ShouldFailWithConflict()
    {
        var (contract, milestone1) = CreateTestContractAndMilestone(MilestoneStatus.Funded);
        contract.AddMilestone(milestone1);

        _currentUserMock.Setup(u => u.UserId).Returns(_clientId);
        _contractRepoMock.Setup(c => c.GetByIdWithMilestonesAsync(contract.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contract);

        var handler = new CloseContractCommandHandler(
            _contractRepoMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        var result = await handler.Handle(new CloseContractCommand(contract.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
        result.ErrorMessage.Should().Contain("unsettled");
    }
}
