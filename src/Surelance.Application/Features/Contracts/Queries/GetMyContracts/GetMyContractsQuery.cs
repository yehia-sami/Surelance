using MediatR;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;

namespace Surelance.Application.Features.Contracts;

public record GetMyContractsQuery : IRequest<Result<IReadOnlyList<ContractSummaryDto>>>;

public class GetMyContractsQueryHandler : IRequestHandler<GetMyContractsQuery, Result<IReadOnlyList<ContractSummaryDto>>>
{
    private readonly IContractRepository _contractRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetMyContractsQueryHandler(IContractRepository contractRepository, ICurrentUserService currentUserService)
    {
        _contractRepository = contractRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<ContractSummaryDto>>> Handle(GetMyContractsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue || !_currentUserService.Role.HasValue)
        {
            return Result<IReadOnlyList<ContractSummaryDto>>.Failure("User is not authenticated.");
        }

        var contracts = await _contractRepository.GetUserContractsAsync(
            _currentUserService.UserId.Value,
            _currentUserService.Role.Value,
            cancellationToken);

        var dtos = contracts.Select(c => new ContractSummaryDto(
            c.Id,
            c.Title,
            c.Description,
            c.ClientId,
            c.Client?.FullName,
            c.FreelancerId,
            c.Freelancer?.FullName,
            c.Status,
            c.Milestones.Count,
            c.Milestones.Sum(m => m.Amount),
            c.CreatedAtUtc
        )).ToList();

        return Result<IReadOnlyList<ContractSummaryDto>>.Success(dtos);
    }
}
