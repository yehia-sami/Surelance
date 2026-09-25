using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Surelance.Domain.Entities;

namespace Surelance.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.Role)
            .IsRequired();

        builder.Ignore(u => u.DomainEvents);
    }
}

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contracts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.HasOne(c => c.Client)
            .WithMany(u => u.ClientContracts)
            .HasForeignKey(c => c.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Freelancer)
            .WithMany(u => u.FreelancerContracts)
            .HasForeignKey(c => c.FreelancerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Milestones)
            .WithOne(m => m.Contract)
            .HasForeignKey(m => m.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(c => c.DomainEvents);
    }
}

public class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> builder)
    {
        builder.ToTable("Milestones");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(m => m.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(m => m.Status)
            .IsRequired();

        builder.HasMany(m => m.LedgerEntries)
            .WithOne(l => l.Milestone)
            .HasForeignKey(l => l.MilestoneId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Disputes)
            .WithOne(d => d.Milestone)
            .HasForeignKey(d => d.MilestoneId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(m => m.RowVersion)
            .IsRowVersion();

        builder.Ignore(m => m.DomainEvents);
    }
}

public class EscrowLedgerEntryConfiguration : IEntityTypeConfiguration<EscrowLedgerEntry>
{
    public void Configure(EntityTypeBuilder<EscrowLedgerEntry> builder)
    {
        builder.ToTable("EscrowLedgerEntries");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(l => l.EntryType)
            .IsRequired();

        builder.Property(l => l.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(l => l.MilestoneId);
        builder.HasIndex(l => l.CreatedAtUtc);

        builder.Ignore(l => l.DomainEvents);
    }
}

public class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        builder.ToTable("Disputes");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Reason)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(d => d.ResolutionNotes)
            .HasMaxLength(2000);

        builder.HasOne(d => d.RaisedByUser)
            .WithMany()
            .HasForeignKey(d => d.RaisedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Arbitrator)
            .WithMany()
            .HasForeignKey(d => d.ArbitratorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => d.SlaExpiresAtUtc);

        builder.Property(d => d.RowVersion)
            .IsRowVersion();

        builder.Ignore(d => d.DomainEvents);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Details)
            .IsRequired();

        builder.HasIndex(a => a.TimestampUtc);
        builder.HasIndex(a => a.UserId);

    }
}

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Type)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(o => o.Content)
            .IsRequired();

        builder.HasIndex(o => o.ProcessedOnUtc);
    }
}
