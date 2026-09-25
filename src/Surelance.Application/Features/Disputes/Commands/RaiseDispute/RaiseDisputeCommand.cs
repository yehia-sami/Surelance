using FluentValidation;
using MediatR;
using Surelance.Application.Common.Behaviors;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Domain.Entities;
using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Disputes;

public record RaiseDisputeCommand(Guid MilestoneId, string Reason) : IRequest<Result<Guid>>, IAuditableCommand
{
    public string EntityName => "Milestone";
    public string GetEntityId() => MilestoneId.ToString();
}

public class RaiseDisputeCommandValidator : AbstractValidator<RaiseDisputeCommand>
{
    public RaiseDisputeCommandValidator()
    {
        RuleFor(x => x.MilestoneId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}

public class RaiseDisputeCommandHandler : IRequestHandler<RaiseDisputeCommand, Result<Guid>>
{
    private readonly IMilestoneRepository _milestoneRepository;
    private readonly IDisputeRepository _disputeRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public RaiseDisputeCommandHandler(
        IMilestoneRepository milestoneRepository,
        IDisputeRepository disputeRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _milestoneRepository = milestoneRepository;
        _disputeRepository = disputeRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(RaiseDisputeCommand request, CancellationToken cancellationToken)
    {
        var milestone = await _milestoneRepository.GetByIdWithContractAsync(request.MilestoneId, cancellationToken);
        if (milestone == null)
        {
            return Result<Guid>.NotFound($"Milestone with ID '{request.MilestoneId}' was not found.");
        }

        if (!_currentUserService.UserId.HasValue)
        {
            return Result<Guid>.Failure("User is not authenticated.");
        }

        var userId = _currentUserService.UserId.Value;
        if (milestone.Contract?.ClientId != userId && milestone.Contract?.FreelancerId != userId)
        {
            return Result<Guid>.Forbidden("Only the client or freelancer on this contract can raise a dispute.");
        }

        if (milestone.Status != MilestoneStatus.Submitted)
        {
            return Result<Guid>.Conflict($"Cannot raise dispute on milestone in status '{milestone.Status}'. Must be in 'Submitted' status.");
        }

        var nowUtc = _dateTimeProvider.UtcNow;
        var slaExpiresAtUtc = nowUtc.AddHours(48);

        try
        {
            var dispute = new Dispute(
                id: Guid.NewGuid(),
                milestoneId: milestone.Id,
                raisedByUserId: userId,
                reason: request.Reason,
                slaExpiresAtUtc: slaExpiresAtUtc,
                createdAtUtc: nowUtc);

            milestone.MarkDisputed(dispute.Id, userId, nowUtc);

            await _disputeRepository.AddAsync(dispute, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(dispute.Id);
        }
        catch (InvalidOperationException ex)
        {
            return Result<Guid>.Conflict(ex.Message);
        }
    }
}
