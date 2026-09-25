using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Surelance.Application.Common.Interfaces;
using Surelance.Domain.Entities;
using Surelance.Domain.Enums;
using Surelance.Infrastructure.Outbox;

namespace Surelance.Infrastructure.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly SurelanceDbContext _context;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly Microsoft.Extensions.Logging.ILogger<UnitOfWork> _logger;

    public UnitOfWork(
        SurelanceDbContext context,
        IBackgroundJobClient backgroundJobClient,
        Microsoft.Extensions.Logging.ILogger<UnitOfWork> logger)
    {
        _context = context;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _context.SaveChangesAsync(cancellationToken);
        if (result > 0)
        {
            // Try immediate outbox processing; if Hangfire is down or in tests, the recurring backup job handles it
            try
            {
                _backgroundJobClient.Enqueue<IOutboxProcessor>(processor => processor.ProcessOutboxMessagesAsync(default));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enqueue immediate outbox processing job. The recurring Hangfire dispatcher will process pending outbox messages.");
            }
        }
        return result;
    }
}

public class ContractRepository : IContractRepository
{
    private readonly SurelanceDbContext _context;

    public ContractRepository(SurelanceDbContext context)
    {
        _context = context;
    }

    public async Task<Contract?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Contracts.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Contract?> GetByIdWithMilestonesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Contracts
            .Include(c => c.Client)
            .Include(c => c.Freelancer)
            .Include(c => c.Milestones)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Contract>> GetUserContractsAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default)
    {
        IQueryable<Contract> query = _context.Contracts
            .Include(c => c.Client)
            .Include(c => c.Freelancer)
            .Include(c => c.Milestones);

        if (role == UserRole.Client)
        {
            query = query.Where(c => c.ClientId == userId);
        }
        else if (role == UserRole.Freelancer)
        {
            query = query.Where(c => c.FreelancerId == userId);
        }

        return await query.OrderByDescending(c => c.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Contract contract, CancellationToken cancellationToken = default)
    {
        await _context.Contracts.AddAsync(contract, cancellationToken);
    }

    public void Update(Contract contract)
    {
        _context.Contracts.Update(contract);
    }
}

public class MilestoneRepository : IMilestoneRepository
{
    private readonly SurelanceDbContext _context;

    public MilestoneRepository(SurelanceDbContext context)
    {
        _context = context;
    }

    public async Task<Milestone?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Milestones.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Milestone?> GetByIdWithContractAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Milestones
            .Include(m => m.Contract)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<Milestone?> GetByIdWithLedgerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Milestones
            .Include(m => m.Contract)
            .Include(m => m.LedgerEntries)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Milestone>> GetPendingFundedPastDeadlineAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        return await _context.Milestones
            .Include(m => m.Contract)
            .Where(m => m.Status == MilestoneStatus.Funded && m.DueDateUtc.HasValue && m.DueDateUtc.Value < nowUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Milestone>> GetSubmittedBeforeAsync(DateTime submittedBeforeUtc, CancellationToken cancellationToken = default)
    {
        return await _context.Milestones
            .Include(m => m.Contract)
            .Where(m => m.Status == MilestoneStatus.Submitted && m.SubmittedAtUtc.HasValue && m.SubmittedAtUtc.Value <= submittedBeforeUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Milestone milestone, CancellationToken cancellationToken = default)
    {
        await _context.Milestones.AddAsync(milestone, cancellationToken);
    }

    public void Update(Milestone milestone)
    {
        _context.Milestones.Update(milestone);
    }
}

public class EscrowLedgerRepository : IEscrowLedgerRepository
{
    private readonly SurelanceDbContext _context;

    public EscrowLedgerRepository(SurelanceDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<EscrowLedgerEntry>> GetByMilestoneIdAsync(Guid milestoneId, CancellationToken cancellationToken = default)
    {
        return await _context.EscrowLedgerEntries
            .Where(e => e.MilestoneId == milestoneId)
            .OrderBy(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetMilestoneBalanceAsync(Guid milestoneId, CancellationToken cancellationToken = default)
    {
        var entries = await _context.EscrowLedgerEntries
            .Where(e => e.MilestoneId == milestoneId)
            .ToListAsync(cancellationToken);

        decimal balance = 0;
        foreach (var entry in entries)
        {
            if (entry.EntryType == LedgerEntryType.Fund)
                balance += entry.Amount;
            else if (entry.EntryType is LedgerEntryType.Release or LedgerEntryType.Refund)
                balance -= entry.Amount;
        }

        return balance;
    }

    public async Task AddAsync(EscrowLedgerEntry entry, CancellationToken cancellationToken = default)
    {
        await _context.EscrowLedgerEntries.AddAsync(entry, cancellationToken);
    }
}

public class DisputeRepository : IDisputeRepository
{
    private readonly SurelanceDbContext _context;

    public DisputeRepository(SurelanceDbContext context)
    {
        _context = context;
    }

    public async Task<Dispute?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Disputes.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Dispute?> GetByIdWithMilestoneAndContractAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Disputes
            .Include(d => d.Milestone)
                .ThenInclude(m => m!.Contract)
            .Include(d => d.RaisedByUser)
            .Include(d => d.Arbitrator)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Dispute>> GetExpiredOpenDisputesAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        return await _context.Disputes
            .Include(d => d.Milestone)
                .ThenInclude(m => m!.Contract)
            .Where(d => d.Status == DisputeStatus.Open && d.SlaExpiresAtUtc <= nowUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Dispute>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Disputes
            .Include(d => d.Milestone)
                .ThenInclude(m => m!.Contract)
            .Include(d => d.RaisedByUser)
            .Include(d => d.Arbitrator)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Dispute dispute, CancellationToken cancellationToken = default)
    {
        await _context.Disputes.AddAsync(dispute, cancellationToken);
    }

    public void Update(Dispute dispute)
    {
        _context.Disputes.Update(dispute);
    }
}

public class UserRepository : IUserRepository
{
    private readonly SurelanceDbContext _context;

    public UserRepository(SurelanceDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users.AnyAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }
}

public class AuditLogRepository : IAuditLogRepository
{
    private readonly SurelanceDbContext _context;

    public AuditLogRepository(SurelanceDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        await _context.AuditLogs.AddAsync(log, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .OrderByDescending(a => a.TimestampUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}

public class OutboxRepository : IOutboxRepository
{
    private readonly SurelanceDbContext _context;

    public OutboxRepository(SurelanceDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _context.OutboxMessages.AddAsync(message, cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedMessagesAsync(DateTime nowUtc, int batchSize = 20, CancellationToken cancellationToken = default)
    {
        return await _context.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null
                     && m.RetryCount < 10
                     && (m.LastFailedAtUtc == null || m.LastFailedAtUtc.Value.AddMinutes(m.RetryCount * 2) <= nowUtc))
            .OrderBy(m => m.RetryCount)
            .ThenBy(m => m.OccurredOnUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}
