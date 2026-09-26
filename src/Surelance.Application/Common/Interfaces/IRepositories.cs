using Surelance.Domain.Entities;
using Surelance.Domain.Enums;

namespace Surelance.Application.Common.Interfaces;

public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Contract?> GetByIdWithMilestonesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contract>> GetUserContractsAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default);
    Task AddAsync(Contract contract, CancellationToken cancellationToken = default);
}

public interface IMilestoneRepository
{
    Task<Milestone?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Milestone?> GetByIdWithContractAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Milestone>> GetPendingFundedPastDeadlineAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Milestone>> GetSubmittedBeforeAsync(DateTime submittedBeforeUtc, CancellationToken cancellationToken = default);
    Task AddAsync(Milestone milestone, CancellationToken cancellationToken = default);
}

public interface IEscrowLedgerRepository
{
    Task<IReadOnlyList<EscrowLedgerEntry>> GetByMilestoneIdAsync(Guid milestoneId, CancellationToken cancellationToken = default);
    Task AddAsync(EscrowLedgerEntry entry, CancellationToken cancellationToken = default);
}

public interface IDisputeRepository
{
    Task<Dispute?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Dispute?> GetByIdWithMilestoneAndContractAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Dispute>> GetExpiredOpenDisputesAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Dispute>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Dispute>> GetForParticipantAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Dispute dispute, CancellationToken cancellationToken = default);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken cancellationToken = default);
}

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedMessagesAsync(DateTime nowUtc, int batchSize = 20, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // Drops any pending (unsaved) entity changes, e.g. after a handler failed midway
    void DiscardChanges();
}
