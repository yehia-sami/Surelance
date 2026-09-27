using FluentValidation;
using MediatR;
using Surelance.Application.Common.Behaviors;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Domain.Entities;
using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Contracts;

public record CreateContractCommand(
    string Title,
    string Description,
    Guid FreelancerId) : IRequest<Result<Guid>>, IAuditableCommand
{
    public string EntityName => "Contract";
    public string GetEntityId() => "New";
}

public class CreateContractCommandValidator : AbstractValidator<CreateContractCommand>
{
    public CreateContractCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.FreelancerId).NotEmpty();
    }
}

public class CreateContractCommandHandler : IRequestHandler<CreateContractCommand, Result<Guid>>
{
    private readonly IContractRepository _contractRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public CreateContractCommandHandler(
        IContractRepository contractRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _contractRepository = contractRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateContractCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != UserRole.Client || !_currentUserService.UserId.HasValue)
        {
            return Result<Guid>.Forbidden("Only clients can create contracts.");
        }

        var freelancer = await _userRepository.GetByIdAsync(request.FreelancerId, cancellationToken);
        if (freelancer == null || freelancer.Role != UserRole.Freelancer)
        {
            return Result<Guid>.NotFound("The specified freelancer does not exist or is not registered as a freelancer.");
        }

        if (freelancer.Id == _currentUserService.UserId.Value)
        {
            return Result<Guid>.Failure("Client and freelancer cannot be the same user.");
        }

        var contract = new Contract(
            id: Guid.NewGuid(),
            title: request.Title,
            description: request.Description,
            clientId: _currentUserService.UserId.Value,
            freelancerId: request.FreelancerId,
            createdAtUtc: _dateTimeProvider.UtcNow);

        await _contractRepository.AddAsync(contract, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(contract.Id);
    }
}
