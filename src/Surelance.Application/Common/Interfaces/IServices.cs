using Surelance.Domain.Entities;
using Surelance.Domain.Enums;

namespace Surelance.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
}

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}

public interface IMilestoneNotifier
{
    Task NotifyMilestoneFundedAsync(Guid milestoneId, Guid contractId, decimal amount, CancellationToken cancellationToken = default);
    Task NotifyMilestoneSubmittedAsync(Guid milestoneId, Guid contractId, CancellationToken cancellationToken = default);
    Task NotifyMilestoneApprovedAsync(Guid milestoneId, Guid contractId, decimal amount, CancellationToken cancellationToken = default);
    Task NotifyDisputeRaisedAsync(Guid milestoneId, Guid disputeId, CancellationToken cancellationToken = default);
    Task NotifyDisputeResolvedAsync(Guid disputeId, Guid milestoneId, DisputeResolution resolution, CancellationToken cancellationToken = default);
    Task SendDeadlineReminderAsync(Guid milestoneId, string milestoneTitle, CancellationToken cancellationToken = default);
}

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
