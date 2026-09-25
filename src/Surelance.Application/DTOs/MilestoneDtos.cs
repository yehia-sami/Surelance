using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Milestones;

public record MilestoneLedgerEntryDto(
    Guid Id,
    Guid MilestoneId,
    decimal Amount,
    LedgerEntryType EntryType,
    string Description,
    DateTime CreatedAtUtc,
    Guid? CreatedByUserId);

public record MilestoneLedgerDto(
    Guid MilestoneId,
    string MilestoneTitle,
    decimal MilestoneTargetAmount,
    decimal CurrentEscrowBalance,
    IReadOnlyList<MilestoneLedgerEntryDto> Entries);
