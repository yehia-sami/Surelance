using FluentValidation;
using MediatR;
using Surelance.Application.Common.Behaviors;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Milestones;

public record SubmitMilestoneCommand(Guid MilestoneId) : IRequest<Result>, IAuditableCommand
{
    public string EntityName => "Milestone";
    public string GetEntityId() => MilestoneId.ToString();
}

public class SubmitMilestoneCommandValidator : AbstractValidator<SubmitMilestoneCommand>
{
    public SubmitMilestoneCommandValidator()
    {
        RuleFor(x => x.MilestoneId).NotEmpty();
    }
}

public class SubmitMilestoneCommandHandler : IRequestHandler<SubmitMilestoneCommand, Result>
{
    private readonly IMilestoneRepository _milestoneRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitMilestoneCommandHandler(
        IMilestoneRepository milestoneRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _milestoneRepository = milestoneRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(SubmitMilestoneCommand request, CancellationToken cancellationToken)
    {
        var milestone = await _milestoneRepository.GetByIdWithContractAsync(request.MilestoneId, cancellationToken);
        if (milestone == null)
        {
            return Result.NotFound($"Milestone with ID '{request.MilestoneId}' was not found.");
        }

        if (!_currentUserService.UserId.HasValue || milestone.Contract?.FreelancerId != _currentUserService.UserId.Value)
        {
            return Result.Forbidden("Only the assigned freelancer on this contract can submit this milestone.");
        }

        if (milestone.Status != MilestoneStatus.Funded)
        {
            return Result.Conflict($"Cannot submit milestone in status '{milestone.Status}'. Milestone must be 'Funded' first.");
        }

        var nowUtc = _dateTimeProvider.UtcNow;

        try
        {
            milestone.Submit(_currentUserService.UserId.Value, nowUtc);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Conflict(ex.Message);
        }
    }
}
