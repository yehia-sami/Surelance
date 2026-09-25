using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Disputes;

public record DisputeDto(
    Guid Id,
    Guid MilestoneId,
    string? MilestoneTitle,
    Guid RaisedByUserId,
    string? RaisedByUserName,
    string Reason,
    DisputeStatus Status,
    DisputeResolution Resolution,
    string? ResolutionNotes,
    Guid? ArbitratorId,
    string? ArbitratorName,
    DateTime CreatedAtUtc,
    DateTime SlaExpiresAtUtc,
    DateTime? ResolvedAtUtc);
