using FluentValidation;
using MediatR;
using Surelance.Application.Common.Behaviors;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Domain.Entities;
using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Disputes;

public record ResolveDisputeCommand(
    Guid DisputeId,
    DisputeResolution Resolution,
    string Notes) : IRequest<Result>, IAuditableCommand
{
    public string EntityName => "Dispute";
    public string GetEntityId() => DisputeId.ToString();
}

public class ResolveDisputeCommandValidator : AbstractValidator<ResolveDisputeCommand>
{
    public ResolveDisputeCommandValidator()
    {
        RuleFor(x => x.DisputeId).NotEmpty();
        RuleFor(x => x.Resolution)
            .Must(r => r is DisputeResolution.RefundClient or DisputeResolution.ReleaseFreelancer)
            .WithMessage("Resolution must be either 'RefundClient' or 'ReleaseFreelancer'.");
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(2000);
    }
}

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand, Result>
{
    private readonly IDisputeRepository _disputeRepository;
    private readonly IEscrowLedgerRepository _escrowLedgerRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ResolveDisputeCommandHandler(
        IDisputeRepository disputeRepository,
        IEscrowLedgerRepository escrowLedgerRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _disputeRepository = disputeRepository;
        _escrowLedgerRepository = escrowLedgerRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ResolveDisputeCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != UserRole.Arbitrator || !_currentUserService.UserId.HasValue)
        {
            return Result.Forbidden("Only an arbitrator can resolve disputes.");
        }

        var dispute = await _disputeRepository.GetByIdWithMilestoneAndContractAsync(request.DisputeId, cancellationToken);
        if (dispute == null)
        {
            return Result.NotFound($"Dispute with ID '{request.DisputeId}' was not found.");
        }

        if (dispute.Status != DisputeStatus.Open)
        {
            return Result.Conflict($"Cannot resolve dispute in status '{dispute.Status}'. Dispute is already closed.");
        }

        var milestone = dispute.Milestone;
        if (milestone == null)
        {
            return Result.NotFound("Associated milestone was not found.");
        }

        var nowUtc = _dateTimeProvider.UtcNow;
        var arbitratorId = _currentUserService.UserId.Value;

        try
        {
            dispute.Resolve(request.Resolution, request.Notes, arbitratorId, nowUtc);
            milestone.Resolve(request.Resolution, nowUtc);

            var entryType = request.Resolution == DisputeResolution.RefundClient ? LedgerEntryType.Refund : LedgerEntryType.Release;
            var description = request.Resolution == DisputeResolution.RefundClient
                ? $"Arbitrator resolved dispute {dispute.Id}: Refunded {milestone.Amount:C} to client. Notes: {request.Notes}"
                : $"Arbitrator resolved dispute {dispute.Id}: Released {milestone.Amount:C} to freelancer. Notes: {request.Notes}";

            var ledgerEntry = new EscrowLedgerEntry(
                id: Guid.NewGuid(),
                milestoneId: milestone.Id,
                amount: milestone.Amount,
                entryType: entryType,
                description: description,
                createdAtUtc: nowUtc,
                createdByUserId: arbitratorId);

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
