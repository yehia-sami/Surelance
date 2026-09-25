using MediatR;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Milestones;

public record GetMilestoneLedgerQuery(Guid MilestoneId) : IRequest<Result<MilestoneLedgerDto>>;

public class GetMilestoneLedgerQueryHandler : IRequestHandler<GetMilestoneLedgerQuery, Result<MilestoneLedgerDto>>
{
    private readonly IMilestoneRepository _milestoneRepository;
    private readonly IEscrowLedgerRepository _escrowLedgerRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetMilestoneLedgerQueryHandler(
        IMilestoneRepository milestoneRepository,
        IEscrowLedgerRepository escrowLedgerRepository,
        ICurrentUserService currentUserService)
    {
        _milestoneRepository = milestoneRepository;
        _escrowLedgerRepository = escrowLedgerRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<MilestoneLedgerDto>> Handle(GetMilestoneLedgerQuery request, CancellationToken cancellationToken)
    {
        var milestone = await _milestoneRepository.GetByIdWithContractAsync(request.MilestoneId, cancellationToken);
        if (milestone == null)
        {
            return Result<MilestoneLedgerDto>.NotFound($"Milestone '{request.MilestoneId}' was not found.");
        }

        if (_currentUserService.Role != UserRole.Arbitrator &&
            milestone.Contract?.ClientId != _currentUserService.UserId &&
            milestone.Contract?.FreelancerId != _currentUserService.UserId)
        {
            return Result<MilestoneLedgerDto>.Forbidden("You are not authorized to view this milestone's ledger.");
        }

        var entries = await _escrowLedgerRepository.GetByMilestoneIdAsync(request.MilestoneId, cancellationToken);

        decimal balance = 0;
        foreach (var entry in entries)
        {
            if (entry.EntryType == LedgerEntryType.Fund)
                balance += entry.Amount;
            else if (entry.EntryType is LedgerEntryType.Release or LedgerEntryType.Refund)
                balance -= entry.Amount;
        }

        var dtos = entries.Select(e => new MilestoneLedgerEntryDto(
            e.Id,
            e.MilestoneId,
            e.Amount,
            e.EntryType,
            e.Description,
            e.CreatedAtUtc,
            e.CreatedByUserId
        )).ToList();

        var resultDto = new MilestoneLedgerDto(
            milestone.Id,
            milestone.Title,
            milestone.Amount,
            balance,
            dtos);

        return Result<MilestoneLedgerDto>.Success(resultDto);
    }
}
