using FluentValidation;
using MediatR;
using Surelance.Application.Common.Behaviors;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;

namespace Surelance.Application.Features.Contracts;

public record CloseContractCommand(Guid ContractId) : IRequest<Result>, IAuditableCommand
{
    public string EntityName => "Contract";
    public string GetEntityId() => ContractId.ToString();
}

public class CloseContractCommandValidator : AbstractValidator<CloseContractCommand>
{
    public CloseContractCommandValidator()
    {
        RuleFor(x => x.ContractId).NotEmpty();
    }
}

public class CloseContractCommandHandler : IRequestHandler<CloseContractCommand, Result>
{
    private readonly IContractRepository _contractRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public CloseContractCommandHandler(
        IContractRepository contractRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _contractRepository = contractRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CloseContractCommand request, CancellationToken cancellationToken)
    {
        var contract = await _contractRepository.GetByIdWithMilestonesAsync(request.ContractId, cancellationToken);
        if (contract == null)
        {
            return Result.NotFound($"Contract with ID '{request.ContractId}' was not found.");
        }

        if (contract.ClientId != _currentUserService.UserId)
        {
            return Result.Forbidden("Only the contract client can close this contract.");
        }

        try
        {
            contract.Close();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Conflict(ex.Message);
        }
    }
}
