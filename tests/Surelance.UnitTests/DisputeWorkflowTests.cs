using FluentAssertions;
using Moq;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Application.Features.Disputes;
using Surelance.Domain.Entities;
using Surelance.Domain.Enums;
using Xunit;

namespace Surelance.UnitTests;

public class DisputeWorkflowTests
{
    private readonly Mock<IDisputeRepository> _disputeRepoMock;
    private readonly Mock<IMilestoneRepository> _milestoneRepoMock;
    private readonly Mock<IEscrowLedgerRepository> _ledgerRepoMock;
    private readonly Mock<IContractRepository> _contractRepoMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IDateTimeProvider> _dateTimeMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    private readonly Guid _clientId = Guid.NewGuid();
    private readonly Guid _freelancerId = Guid.NewGuid();
    private readonly Guid _arbitratorId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public DisputeWorkflowTests()
    {
        _disputeRepoMock = new Mock<IDisputeRepository>();
        _milestoneRepoMock = new Mock<IMilestoneRepository>();
        _ledgerRepoMock = new Mock<IEscrowLedgerRepository>();
        _contractRepoMock = new Mock<IContractRepository>();
        _currentUserMock = new Mock<ICurrentUserService>();
        _dateTimeMock = new Mock<IDateTimeProvider>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _dateTimeMock.Setup(d => d.UtcNow).Returns(_now);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private (Contract contract, Milestone milestone) CreateSubmittedMilestone()
    {
        var contract = new Contract(Guid.NewGuid(), "Test Contract", "Desc", _clientId, _freelancerId, _now);
        contract.Accept();

        var milestone = new Milestone(Guid.NewGuid(), contract.Id, "Milestone 1", "Desc", 2000m, _now.AddDays(7), _now);
        milestone.Fund(_clientId, _now);
        milestone.Submit(_freelancerId, _now);
        milestone.ClearDomainEvents();

        milestone.SetContract(contract);
        contract.AddMilestone(milestone);

        return (contract, milestone);
    }

    [Fact]
    public async Task RaiseDispute_OnSubmittedMilestone_ShouldSucceedAndSetStatusToDisputed()
    {
        var (_, milestone) = CreateSubmittedMilestone();
        _currentUserMock.Setup(u => u.UserId).Returns(_clientId);
        _currentUserMock.Setup(u => u.Role).Returns(UserRole.Client);

        _milestoneRepoMock.Setup(r => r.GetByIdWithContractAsync(milestone.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(milestone);

        var handler = new RaiseDisputeCommandHandler(
            _milestoneRepoMock.Object,
            _disputeRepoMock.Object,
            _currentUserMock.Object,
            _dateTimeMock.Object,
            _unitOfWorkMock.Object);

        var result = await handler.Handle(new RaiseDisputeCommand(milestone.Id, "Deliverable does not meet specification requirements."), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        milestone.Status.Should().Be(MilestoneStatus.Disputed);

        _disputeRepoMock.Verify(d => d.AddAsync(
            It.Is<Dispute>(disp => 
                disp.MilestoneId == milestone.Id && 
                disp.RaisedByUserId == _clientId &&
                disp.Status == DisputeStatus.Open &&
                disp.SlaExpiresAtUtc == _now.AddHours(48)), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveDispute_WhenArbitratorRefundsClient_ShouldCreateRefundLedgerEntry()
    {
        var (contract, milestone) = CreateSubmittedMilestone();
        milestone.MarkDisputed(Guid.NewGuid(), _clientId, _now);

        var dispute = new Dispute(Guid.NewGuid(), milestone.Id, _clientId, "Reason", _now.AddDays(5), _now);
        dispute.SetMilestone(milestone);

        _currentUserMock.Setup(u => u.UserId).Returns(_arbitratorId);
        _currentUserMock.Setup(u => u.Role).Returns(UserRole.Arbitrator);

        _disputeRepoMock.Setup(d => d.GetByIdWithMilestoneAndContractAsync(dispute.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispute);
        _contractRepoMock.Setup(c => c.GetByIdWithMilestonesAsync(contract.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contract);

        var handler = new ResolveDisputeCommandHandler(
            _disputeRepoMock.Object,
            _ledgerRepoMock.Object,
            _currentUserMock.Object,
            _dateTimeMock.Object,
            _unitOfWorkMock.Object);

        var result = await handler.Handle(new ResolveDisputeCommand(
            dispute.Id, 
            DisputeResolution.RefundClient, 
            "Deliverable was not according to acceptance criteria."), 
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        dispute.Status.Should().Be(DisputeStatus.Resolved);
        dispute.Resolution.Should().Be(DisputeResolution.RefundClient);
        milestone.Status.Should().Be(MilestoneStatus.Refunded);

        _ledgerRepoMock.Verify(l => l.AddAsync(
            It.Is<EscrowLedgerEntry>(e => 
                e.MilestoneId == milestone.Id && 
                e.Amount == 2000m && 
                e.EntryType == LedgerEntryType.Refund && 
                e.CreatedByUserId == _arbitratorId), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveDispute_WhenArbitratorReleasesFreelancer_ShouldCreateReleaseLedgerEntry()
    {
        var (contract, milestone) = CreateSubmittedMilestone();
        milestone.MarkDisputed(Guid.NewGuid(), _clientId, _now);

        var dispute = new Dispute(Guid.NewGuid(), milestone.Id, _clientId, "Reason", _now.AddDays(5), _now);
        dispute.SetMilestone(milestone);

        _currentUserMock.Setup(u => u.UserId).Returns(_arbitratorId);
        _currentUserMock.Setup(u => u.Role).Returns(UserRole.Arbitrator);

        _disputeRepoMock.Setup(d => d.GetByIdWithMilestoneAndContractAsync(dispute.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispute);
        _contractRepoMock.Setup(c => c.GetByIdWithMilestonesAsync(contract.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contract);

        var handler = new ResolveDisputeCommandHandler(
            _disputeRepoMock.Object,
            _ledgerRepoMock.Object,
            _currentUserMock.Object,
            _dateTimeMock.Object,
            _unitOfWorkMock.Object);

        var result = await handler.Handle(new ResolveDisputeCommand(
            dispute.Id, 
            DisputeResolution.ReleaseFreelancer, 
            "Freelancer met all deliverables according to the agreed contract."), 
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        dispute.Status.Should().Be(DisputeStatus.Resolved);
        dispute.Resolution.Should().Be(DisputeResolution.ReleaseFreelancer);

        _ledgerRepoMock.Verify(l => l.AddAsync(
            It.Is<EscrowLedgerEntry>(e => 
                e.MilestoneId == milestone.Id && 
                e.Amount == 2000m && 
                e.EntryType == LedgerEntryType.Release && 
                e.CreatedByUserId == _arbitratorId), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckDisputeSla_WhenSlaExpired_ShouldAutoResolveWithDefaultRule()
    {
        var (contract, milestone) = CreateSubmittedMilestone();
        milestone.MarkDisputed(Guid.NewGuid(), _clientId, _now.AddDays(-6));

        var expiredDispute = new Dispute(
            Guid.NewGuid(), 
            milestone.Id, 
            _clientId, 
            "Client dispute", 
            _now.AddDays(-1), 
            _now.AddDays(-6));

        expiredDispute.SetMilestone(milestone);

        _disputeRepoMock.Setup(d => d.GetExpiredOpenDisputesAsync(_now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Dispute> { expiredDispute });

        _milestoneRepoMock.Setup(m => m.GetByIdWithContractAsync(milestone.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(milestone);

        var handler = new CheckDisputeSlaCommandHandler(
            _disputeRepoMock.Object,
            _ledgerRepoMock.Object,
            _dateTimeMock.Object,
            _unitOfWorkMock.Object);

        var result = await handler.Handle(new CheckDisputeSlaCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);

        expiredDispute.Status.Should().Be(DisputeStatus.Resolved);
        // SLA timeout always refunds the client regardless of who raised it
        expiredDispute.Resolution.Should().Be(DisputeResolution.RefundClient);
        milestone.Status.Should().Be(MilestoneStatus.Refunded);

        _ledgerRepoMock.Verify(l => l.AddAsync(
            It.Is<EscrowLedgerEntry>(e => 
                e.MilestoneId == milestone.Id && 
                e.EntryType == LedgerEntryType.Refund && 
                e.Description.Contains("Auto-SLA Resolution")), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtendDisputeSla_WhenDisputeIsOpenAndCalledByArbitrator_ShouldSucceedAndMoveDeadlineForward()
    {
        var (_, milestone) = CreateSubmittedMilestone();
        var dispute = new Dispute(Guid.NewGuid(), milestone.Id, _clientId, "Reason", _now.AddHours(24), _now);

        _currentUserMock.Setup(u => u.UserId).Returns(_arbitratorId);
        _currentUserMock.Setup(u => u.Role).Returns(UserRole.Arbitrator);

        _disputeRepoMock.Setup(d => d.GetByIdAsync(dispute.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispute);

        var handler = new ExtendDisputeSlaCommandHandler(
            _disputeRepoMock.Object,
            _currentUserMock.Object,
            _dateTimeMock.Object,
            _unitOfWorkMock.Object);

        var result = await handler.Handle(new ExtendDisputeSlaCommand(dispute.Id, 48), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        dispute.SlaExpiresAtUtc.Should().Be(_now.AddHours(48));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Dispute_ExtendSla_WhenDeadlineNotLaterThanCurrent_ThrowsInvalidOperationException()
    {
        var dispute = new Dispute(Guid.NewGuid(), Guid.NewGuid(), _clientId, "Reason", _now.AddHours(48), _now);

        var act = () => dispute.ExtendSla(_now.AddHours(24));

        act.Should().Throw<InvalidOperationException>().WithMessage("*must be later than current deadline*");
    }

    [Fact]
    public void Milestone_MarkDisputed_WhenNotSubmitted_ThrowsInvalidOperationException()
    {
        var milestone = new Milestone(Guid.NewGuid(), Guid.NewGuid(), "M1", "Desc", 1000m, _now.AddDays(5), _now);

        var act = () => milestone.MarkDisputed(Guid.NewGuid(), _clientId, _now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Only 'Submitted' milestones can be disputed*");
    }
}
