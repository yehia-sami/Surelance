using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Contracts;

public record ContractSummaryDto(
    Guid Id,
    string Title,
    string Description,
    Guid ClientId,
    string? ClientName,
    Guid FreelancerId,
    string? FreelancerName,
    ContractStatus Status,
    int MilestoneCount,
    decimal TotalAmount,
    DateTime CreatedAtUtc);

public record ContractDetailsDto(
    Guid Id,
    string Title,
    string Description,
    Guid ClientId,
    string? ClientName,
    Guid FreelancerId,
    string? FreelancerName,
    ContractStatus Status,
    DateTime CreatedAtUtc,
    IReadOnlyList<MilestoneDto> Milestones);

public record MilestoneDto(
    Guid Id,
    Guid ContractId,
    string Title,
    string Description,
    decimal Amount,
    MilestoneStatus Status,
    DateTime? DueDateUtc,
    DateTime CreatedAtUtc,
    DateTime? FundedAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime? ReleasedAtUtc);
