using MediatR;
using Microsoft.Extensions.Logging;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Domain.Entities;
using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Disputes;

public record CheckDisputeSlaCommand : IRequest<Result<int>>;

public class CheckDisputeSlaCommandHandler : IRequestHandler<CheckDisputeSlaCommand, Result<int>>
{
    private readonly IDisputeRepository _disputeRepository;
    private readonly IEscrowLedgerRepository _escrowLedgerRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public CheckDisputeSlaCommandHandler(
        IDisputeRepository disputeRepository,
        IEscrowLedgerRepository escrowLedgerRepository,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _disputeRepository = disputeRepository;
        _escrowLedgerRepository = escrowLedgerRepository;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<int>> Handle(CheckDisputeSlaCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _dateTimeProvider.UtcNow;
        var expiredDisputes = await _disputeRepository.GetExpiredOpenDisputesAsync(nowUtc, cancellationToken);

        if (expiredDisputes.Count == 0)
        {
            return Result<int>.Success(0);
        }

        var resolvedCount = 0;
        const string resolutionNotes = "Automated resolution: SLA expired with no arbitrator action. Per policy, escrow defaults back to the client absent a decision.";

        foreach (var dispute in expiredDisputes)
        {
            var milestone = dispute.Milestone;

            // Skip inconsistent records instead of throwing, so one bad row can't block every other expired dispute
            if (milestone == null || milestone.Status != MilestoneStatus.Disputed) continue;

            dispute.Resolve(
                DisputeResolution.RefundClient,
                resolutionNotes,
                arbitratorId: null,
                nowUtc);

            milestone.Resolve(DisputeResolution.RefundClient, nowUtc);

            var ledgerEntry = new EscrowLedgerEntry(
                id: Guid.NewGuid(),
                milestoneId: milestone.Id,
                amount: milestone.Amount,
                entryType: LedgerEntryType.Refund,
                description: $"Auto-SLA Resolution: Arbitrator SLA expired for dispute {dispute.Id}. Refunded {milestone.Amount:C} to client.",
                createdAtUtc: nowUtc,
                createdByUserId: null);

            await _escrowLedgerRepository.AddAsync(ledgerEntry, cancellationToken);
            resolvedCount++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(resolvedCount);
    }
}
