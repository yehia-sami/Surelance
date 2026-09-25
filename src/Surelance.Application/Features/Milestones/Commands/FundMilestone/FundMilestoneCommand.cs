using FluentValidation;
using MediatR;
using Surelance.Application.Common.Behaviors;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Domain.Entities;
using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Milestones;

public record FundMilestoneCommand(Guid MilestoneId) : IRequest<Result>, IAuditableCommand
{
    public string EntityName => "Milestone";
    public string GetEntityId() => MilestoneId.ToString();
}

public class FundMilestoneCommandValidator : AbstractValidator<FundMilestoneCommand>
{
    public FundMilestoneCommandValidator()
    {
        RuleFor(x => x.MilestoneId).NotEmpty();
    }
}

public class FundMilestoneCommandHandler : IRequestHandler<FundMilestoneCommand, Result>
{
    private readonly IMilestoneRepository _milestoneRepository;
    private readonly IEscrowLedgerRepository _escrowLedgerRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public FundMilestoneCommandHandler(
        IMilestoneRepository milestoneRepository,
        IEscrowLedgerRepository escrowLedgerRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _milestoneRepository = milestoneRepository;
        _escrowLedgerRepository = escrowLedgerRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(FundMilestoneCommand request, CancellationToken cancellationToken)
    {
        var milestone = await _milestoneRepository.GetByIdWithContractAsync(request.MilestoneId, cancellationToken);
        if (milestone == null)
        {
            return Result.NotFound($"Milestone with ID '{request.MilestoneId}' was not found.");
        }

        if (!_currentUserService.UserId.HasValue || milestone.Contract?.ClientId != _currentUserService.UserId.Value)
        {
            return Result.Forbidden("Only the client on this contract can fund this milestone.");
        }

        if (milestone.Status != MilestoneStatus.Pending)
        {
            return Result.Conflict($"Cannot fund milestone in status '{milestone.Status}'. Milestone must be in 'Pending' status.");
        }

        var nowUtc = _dateTimeProvider.UtcNow;

        try
        {
            milestone.Fund(_currentUserService.UserId.Value, nowUtc);

            var ledgerEntry = new EscrowLedgerEntry(
                id: Guid.NewGuid(),
                milestoneId: milestone.Id,
                amount: milestone.Amount,
                entryType: LedgerEntryType.Fund,
                description: $"Client funded milestone '{milestone.Title}' with {milestone.Amount:C}",
                createdAtUtc: nowUtc,
                createdByUserId: _currentUserService.UserId);

            await _escrowLedgerRepository.AddAsync(ledgerEntry, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Conflict(ex.Message);
        }
    }
}
