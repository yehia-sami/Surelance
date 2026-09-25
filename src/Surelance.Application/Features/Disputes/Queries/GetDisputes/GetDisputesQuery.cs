using MediatR;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Disputes;

public record GetDisputesQuery : IRequest<Result<IReadOnlyList<DisputeDto>>>;

public class GetDisputesQueryHandler : IRequestHandler<GetDisputesQuery, Result<IReadOnlyList<DisputeDto>>>
{
    private readonly IDisputeRepository _disputeRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetDisputesQueryHandler(IDisputeRepository disputeRepository, ICurrentUserService currentUserService)
    {
        _disputeRepository = disputeRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<DisputeDto>>> Handle(GetDisputesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue || !_currentUserService.Role.HasValue)
        {
            return Result<IReadOnlyList<DisputeDto>>.Failure("User is not authenticated.");
        }

        var allDisputes = await _disputeRepository.GetAllAsync(cancellationToken);
        var userId = _currentUserService.UserId.Value;
        var role = _currentUserService.Role.Value;

        var disputes = role == UserRole.Arbitrator
            ? allDisputes
            : allDisputes.Where(d => d.RaisedByUserId == userId ||
                                     d.Milestone?.Contract?.ClientId == userId ||
                                     d.Milestone?.Contract?.FreelancerId == userId).ToList();

        var dtos = disputes.Select(d => new DisputeDto(
            d.Id,
            d.MilestoneId,
            d.Milestone?.Title,
            d.RaisedByUserId,
            d.RaisedByUser?.FullName,
            d.Reason,
            d.Status,
            d.Resolution,
            d.ResolutionNotes,
            d.ArbitratorId,
            d.Arbitrator?.FullName,
            d.CreatedAtUtc,
            d.SlaExpiresAtUtc,
            d.ResolvedAtUtc
        )).ToList();

        return Result<IReadOnlyList<DisputeDto>>.Success(dtos);
    }
}
