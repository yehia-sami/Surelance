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

        // Arbitrators see every dispute; everyone else only sees disputes on their own contracts (filtered in SQL)
        var disputes = _currentUserService.Role.Value == UserRole.Arbitrator
            ? await _disputeRepository.GetAllAsync(cancellationToken)
            : await _disputeRepository.GetForParticipantAsync(_currentUserService.UserId.Value, cancellationToken);

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
