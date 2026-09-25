using MediatR;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Disputes;

public record GetDisputeByIdQuery(Guid DisputeId) : IRequest<Result<DisputeDto>>;

public class GetDisputeByIdQueryHandler : IRequestHandler<GetDisputeByIdQuery, Result<DisputeDto>>
{
    private readonly IDisputeRepository _disputeRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetDisputeByIdQueryHandler(IDisputeRepository disputeRepository, ICurrentUserService currentUserService)
    {
        _disputeRepository = disputeRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<DisputeDto>> Handle(GetDisputeByIdQuery request, CancellationToken cancellationToken)
    {
        var dispute = await _disputeRepository.GetByIdWithMilestoneAndContractAsync(request.DisputeId, cancellationToken);
        if (dispute == null)
        {
            return Result<DisputeDto>.NotFound($"Dispute with ID '{request.DisputeId}' was not found.");
        }

        var userId = _currentUserService.UserId;
        if (_currentUserService.Role != UserRole.Arbitrator &&
            dispute.Milestone?.Contract?.ClientId != userId &&
            dispute.Milestone?.Contract?.FreelancerId != userId)
        {
            return Result<DisputeDto>.Forbidden("You are not authorized to view this dispute.");
        }

        var dto = new DisputeDto(
            dispute.Id,
            dispute.MilestoneId,
            dispute.Milestone?.Title,
            dispute.RaisedByUserId,
            dispute.RaisedByUser?.FullName,
            dispute.Reason,
            dispute.Status,
            dispute.Resolution,
            dispute.ResolutionNotes,
            dispute.ArbitratorId,
            dispute.Arbitrator?.FullName,
            dispute.CreatedAtUtc,
            dispute.SlaExpiresAtUtc,
            dispute.ResolvedAtUtc);

        return Result<DisputeDto>.Success(dto);
    }
}
