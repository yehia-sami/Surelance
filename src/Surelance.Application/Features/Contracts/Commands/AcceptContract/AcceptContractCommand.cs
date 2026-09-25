using FluentValidation;
using MediatR;
using Surelance.Application.Common.Behaviors;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;

namespace Surelance.Application.Features.Contracts;

public record AcceptContractCommand(Guid ContractId) : IRequest<Result>, IAuditableCommand
{
    public string EntityName => "Contract";
    public string GetEntityId() => ContractId.ToString();
}

public class AcceptContractCommandValidator : AbstractValidator<AcceptContractCommand>
{
    public AcceptContractCommandValidator()
    {
        RuleFor(x => x.ContractId).NotEmpty();
    }
}

public class AcceptContractCommandHandler : IRequestHandler<AcceptContractCommand, Result>
{
    private readonly IContractRepository _contractRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public AcceptContractCommandHandler(
        IContractRepository contractRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _contractRepository = contractRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(AcceptContractCommand request, CancellationToken cancellationToken)
    {
        var contract = await _contractRepository.GetByIdAsync(request.ContractId, cancellationToken);
        if (contract == null)
        {
            return Result.NotFound($"Contract with ID '{request.ContractId}' was not found.");
        }

        if (contract.FreelancerId != _currentUserService.UserId)
        {
            return Result.Forbidden("Only the assigned freelancer can accept this contract.");
        }

        try
        {
            contract.Accept();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Conflict(ex.Message);
        }
    }
}
