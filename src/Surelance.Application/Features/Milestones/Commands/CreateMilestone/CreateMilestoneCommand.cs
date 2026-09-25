using FluentValidation;
using MediatR;
using Surelance.Application.Common.Behaviors;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Domain.Entities;
using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Milestones;

public record CreateMilestoneCommand(
    Guid ContractId,
    string Title,
    string Description,
    decimal Amount,
    DateTime? DueDateUtc) : IRequest<Result<Guid>>, IAuditableCommand
{
    public string EntityName => "Milestone";
    public string GetEntityId() => "New";
}

public class CreateMilestoneCommandValidator : AbstractValidator<CreateMilestoneCommand>
{
    public CreateMilestoneCommandValidator()
    {
        RuleFor(x => x.ContractId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Milestone amount must be greater than zero.");
    }
}

public class CreateMilestoneCommandHandler : IRequestHandler<CreateMilestoneCommand, Result<Guid>>
{
    private readonly IContractRepository _contractRepository;
    private readonly IMilestoneRepository _milestoneRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public CreateMilestoneCommandHandler(
        IContractRepository contractRepository,
        IMilestoneRepository milestoneRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _contractRepository = contractRepository;
        _milestoneRepository = milestoneRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateMilestoneCommand request, CancellationToken cancellationToken)
    {
        var contract = await _contractRepository.GetByIdAsync(request.ContractId, cancellationToken);
        if (contract == null)
        {
            return Result<Guid>.NotFound($"Contract '{request.ContractId}' was not found.");
        }

        if (contract.ClientId != _currentUserService.UserId)
        {
            return Result<Guid>.Forbidden("Only the client on this contract can create milestones.");
        }

        if (contract.Status == ContractStatus.Completed || contract.Status == ContractStatus.Cancelled)
        {
            return Result<Guid>.Conflict($"Cannot add milestones to a contract in status '{contract.Status}'.");
        }

        var milestone = new Milestone(
            id: Guid.NewGuid(),
            contractId: contract.Id,
            title: request.Title,
            description: request.Description,
            amount: request.Amount,
            dueDateUtc: request.DueDateUtc,
            createdAtUtc: _dateTimeProvider.UtcNow);

        await _milestoneRepository.AddAsync(milestone, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(milestone.Id);
    }
}
