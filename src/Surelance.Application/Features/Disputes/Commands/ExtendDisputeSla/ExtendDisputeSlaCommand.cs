using FluentValidation;
using MediatR;
using Surelance.Application.Common.Behaviors;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Disputes;

public record ExtendDisputeSlaCommand(
    Guid DisputeId,
    int AdditionalHours) : IRequest<Result>, IAuditableCommand
{
    public string EntityName => "Dispute";
    public string GetEntityId() => DisputeId.ToString();
}

public class ExtendDisputeSlaCommandValidator : AbstractValidator<ExtendDisputeSlaCommand>
{
    public ExtendDisputeSlaCommandValidator()
    {
        RuleFor(x => x.DisputeId).NotEmpty();
        RuleFor(x => x.AdditionalHours)
            .GreaterThan(0).WithMessage("Additional hours must be greater than zero.")
            .LessThanOrEqualTo(168).WithMessage("Additional hours cannot exceed 168 hours (7 days).");
    }
}

public class ExtendDisputeSlaCommandHandler : IRequestHandler<ExtendDisputeSlaCommand, Result>
{
    private readonly IDisputeRepository _disputeRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ExtendDisputeSlaCommandHandler(
        IDisputeRepository disputeRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _disputeRepository = disputeRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ExtendDisputeSlaCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != UserRole.Arbitrator || !_currentUserService.UserId.HasValue)
        {
            return Result.Forbidden("Only an arbitrator can extend dispute SLA deadlines.");
        }

        var dispute = await _disputeRepository.GetByIdAsync(request.DisputeId, cancellationToken);
        if (dispute == null)
        {
            return Result.NotFound($"Dispute with ID '{request.DisputeId}' was not found.");
        }

        if (dispute.Status != DisputeStatus.Open)
        {
            return Result.Conflict($"Cannot extend SLA for dispute in status '{dispute.Status}'. Only 'Open' disputes can have their SLA extended.");
        }

        var nowUtc = _dateTimeProvider.UtcNow;
        var newDeadline = nowUtc.AddHours(request.AdditionalHours);

        try
        {
            dispute.ExtendSla(newDeadline);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Conflict(ex.Message);
        }
    }
}
