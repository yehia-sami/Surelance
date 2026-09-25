using MediatR;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Contracts;

public record GetContractByIdQuery(Guid ContractId) : IRequest<Result<ContractDetailsDto>>;

public class GetContractByIdQueryHandler : IRequestHandler<GetContractByIdQuery, Result<ContractDetailsDto>>
{
    private readonly IContractRepository _contractRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetContractByIdQueryHandler(IContractRepository contractRepository, ICurrentUserService currentUserService)
    {
        _contractRepository = contractRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ContractDetailsDto>> Handle(GetContractByIdQuery request, CancellationToken cancellationToken)
    {
        var contract = await _contractRepository.GetByIdWithMilestonesAsync(request.ContractId, cancellationToken);
        if (contract == null)
        {
            return Result<ContractDetailsDto>.NotFound($"Contract '{request.ContractId}' not found.");
        }

        if (_currentUserService.Role != UserRole.Arbitrator &&
            contract.ClientId != _currentUserService.UserId &&
            contract.FreelancerId != _currentUserService.UserId)
        {
            return Result<ContractDetailsDto>.Forbidden("You are not authorized to view this contract.");
        }

        var dto = new ContractDetailsDto(
            contract.Id,
            contract.Title,
            contract.Description,
            contract.ClientId,
            contract.Client?.FullName,
            contract.FreelancerId,
            contract.Freelancer?.FullName,
            contract.Status,
            contract.CreatedAtUtc,
            contract.Milestones.Select(m => new MilestoneDto(
                m.Id,
                m.ContractId,
                m.Title,
                m.Description,
                m.Amount,
                m.Status,
                m.DueDateUtc,
                m.CreatedAtUtc,
                m.FundedAtUtc,
                m.SubmittedAtUtc,
                m.ReleasedAtUtc
            )).ToList());

        return Result<ContractDetailsDto>.Success(dto);
    }
}
