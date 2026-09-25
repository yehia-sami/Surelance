using Microsoft.EntityFrameworkCore;
using Surelance.Domain.Common;
using Surelance.Domain.Entities;
using System.Reflection;
using System.Text.Json;

namespace Surelance.Infrastructure.Persistence;

public class SurelanceDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<EscrowLedgerEntry> EscrowLedgerEntries => Set<EscrowLedgerEntry>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public SurelanceDbContext(DbContextOptions<SurelanceDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Save domain events to outbox in the same transaction so they're guaranteed to persist before dispatch
        var domainEntities = ChangeTracker
            .Entries<BaseEntity>()
            .Where(x => x.Entity.DomainEvents.Any())
            .Select(x => x.Entity)
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(x => x.DomainEvents)
            .ToList();

        domainEntities.ForEach(entity => entity.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
            var eventType = domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().FullName!;
            var content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());

            var outboxMessage = new OutboxMessage(
                id: Guid.NewGuid(),
                occurredOnUtc: domainEvent.OccurredOnUtc,
                type: eventType,
                content: content);

            await OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
