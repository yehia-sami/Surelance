using FluentValidation;
using MediatR;
using Surelance.Application.Common.Behaviors;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Domain.Entities;
using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Milestones;

public record ApproveMilestoneCommand(Guid MilestoneId) : IRequest<Result>, IAuditableCommand
{
    public string EntityName => "Milestone";
    public string GetEntityId() => MilestoneId.ToString();
}

public class ApproveMilestoneCommandValidator : AbstractValidator<ApproveMilestoneCommand>
{
    public ApproveMilestoneCommandValidator()
    {
        RuleFor(x => x.MilestoneId).NotEmpty();
    }
}

public class ApproveMilestoneCommandHandler : IRequestHandler<ApproveMilestoneCommand, Result>
{
    private readonly IMilestoneRepository _milestoneRepository;
    private readonly IEscrowLedgerRepository _escrowLedgerRepository;
    private readonly IContractRepository _contractRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveMilestoneCommandHandler(
        IMilestoneRepository milestoneRepository,
        IEscrowLedgerRepository escrowLedgerRepository,
        IContractRepository contractRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _milestoneRepository = milestoneRepository;
        _escrowLedgerRepository = escrowLedgerRepository;
        _contractRepository = contractRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ApproveMilestoneCommand request, CancellationToken cancellationToken)
    {
        var milestone = await _milestoneRepository.GetByIdWithContractAsync(request.MilestoneId, cancellationToken);
        if (milestone == null)
        {
            return Result.NotFound($"Milestone with ID '{request.MilestoneId}' was not found.");
        }

        if (!_currentUserService.UserId.HasValue || milestone.Contract?.ClientId != _currentUserService.UserId.Value)
        {
            return Result.Forbidden("Only the client on this contract can approve this milestone.");
        }

        if (milestone.Status != MilestoneStatus.Submitted)
        {
            return Result.Conflict($"Cannot approve milestone in status '{milestone.Status}'. Milestone must be 'Submitted'.");
        }

        var nowUtc = _dateTimeProvider.UtcNow;
        return await ExecuteApproveCoreAsync(
            milestone,
            _currentUserService.UserId.Value,
            nowUtc,
            _escrowLedgerRepository,
            _unitOfWork,
            customDescription: null,
            createdByUserId: _currentUserService.UserId,
            cancellationToken: cancellationToken);
    }

    public static async Task<Result> ExecuteApproveCoreAsync(
        Milestone milestone,
        Guid clientId,
        DateTime nowUtc,
        IEscrowLedgerRepository escrowLedgerRepository,
        IUnitOfWork unitOfWork,
        string? customDescription = null,
        Guid? createdByUserId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            milestone.Approve(clientId, nowUtc);

            var description = customDescription ?? $"Client approved milestone '{milestone.Title}'. Escrow released {milestone.Amount:C} to freelancer.";

            var ledgerEntry = new EscrowLedgerEntry(
                id: Guid.NewGuid(),
                milestoneId: milestone.Id,
                amount: milestone.Amount,
                entryType: LedgerEntryType.Release,
                description: description,
                createdAtUtc: nowUtc,
                createdByUserId: createdByUserId);

            await escrowLedgerRepository.AddAsync(ledgerEntry, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Conflict(ex.Message);
        }
    }
}
