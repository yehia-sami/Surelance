using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Surelance.Application.Common.Interfaces;
using Surelance.Domain.Entities;
using Surelance.Domain.Enums;
using Surelance.Infrastructure.Persistence;

namespace Surelance.Infrastructure.Seed;

public static class DbInitializer
{
    public static async Task SeedAsync(SurelanceDbContext context, IPasswordHasher passwordHasher, ILogger logger)
    {
        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        if (await context.Users.AnyAsync())
        {
            return;
        }

        logger.LogInformation("Seeding demo data...");

        var nowUtc = DateTime.UtcNow;

        // Demo users
        var clientAlice = new User(
            id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            email: "client@surelance.com",
            passwordHash: passwordHasher.HashPassword("Client@123"),
            fullName: "Alice Client",
            role: UserRole.Client,
            createdAtUtc: nowUtc.AddDays(-30));

        var freelancerBob = new User(
            id: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            email: "freelancer@surelance.com",
            passwordHash: passwordHasher.HashPassword("Freelancer@123"),
            fullName: "Bob Freelancer",
            role: UserRole.Freelancer,
            createdAtUtc: nowUtc.AddDays(-30));

        var freelancerElena = new User(
            id: Guid.Parse("22222222-2222-2222-2222-222222222223"),
            email: "elena@surelance.com",
            passwordHash: passwordHasher.HashPassword("Freelancer@123"),
            fullName: "Elena Architect",
            role: UserRole.Freelancer,
            createdAtUtc: nowUtc.AddDays(-25));

        var arbitratorSarah = new User(
            id: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            email: "arbitrator@surelance.com",
            passwordHash: passwordHasher.HashPassword("Arbitrator@123"),
            fullName: "Sarah Arbitrator",
            role: UserRole.Arbitrator,
            createdAtUtc: nowUtc.AddDays(-30));

        await context.Users.AddRangeAsync(clientAlice, freelancerBob, freelancerElena, arbitratorSarah);

        // Contract 1: active contract with milestones in various stages
        var contract1 = new Contract(
            id: Guid.Parse("44444444-4444-4444-4444-444444444441"),
            title: "E-Commerce Mobile App Development",
            description: "Mobile app build with milestones in various states to test the workflow.",
            clientId: clientAlice.Id,
            freelancerId: freelancerBob.Id,
            createdAtUtc: nowUtc.AddDays(-20));
        contract1.Accept();

        var c1m1 = new Milestone(
            id: Guid.Parse("55555555-5555-5555-5555-555555555511"),
            contractId: contract1.Id,
            title: "UI/UX Wireframes & Technical Spec",
            description: "Initial Figma mockups and technical plan.",
            amount: 1500.00m,
            dueDateUtc: nowUtc.AddDays(-10),
            createdAtUtc: nowUtc.AddDays(-20));
        c1m1.Fund(clientAlice.Id, nowUtc.AddDays(-19));
        c1m1.Submit(freelancerBob.Id, nowUtc.AddDays(-12));
        c1m1.Approve(clientAlice.Id, nowUtc.AddDays(-10));

        var c1m2 = new Milestone(
            id: Guid.Parse("55555555-5555-5555-5555-555555555512"),
            contractId: contract1.Id,
            title: "Backend API & Database Schema",
            description: "Core REST endpoints and persistence.",
            amount: 2500.00m,
            dueDateUtc: nowUtc.AddDays(5),
            createdAtUtc: nowUtc.AddDays(-20));
        c1m2.Fund(clientAlice.Id, nowUtc.AddDays(-10));
        c1m2.Submit(freelancerBob.Id, nowUtc.AddDays(-1));

        var c1m3 = new Milestone(
            id: Guid.Parse("55555555-5555-5555-5555-555555555513"),
            contractId: contract1.Id,
            title: "Frontend Mobile App Integration",
            description: "Connecting screens with the backend services.",
            amount: 2000.00m,
            dueDateUtc: nowUtc.AddDays(15),
            createdAtUtc: nowUtc.AddDays(-20));
        c1m3.Fund(clientAlice.Id, nowUtc.AddDays(-2));

        var c1m4 = new Milestone(
            id: Guid.Parse("55555555-5555-5555-5555-555555555514"),
            contractId: contract1.Id,
            title: "Production Deployment & App Store Submission",
            description: "Final release packaging and store submission.",
            amount: 1000.00m,
            dueDateUtc: nowUtc.AddDays(25),
            createdAtUtc: nowUtc.AddDays(-20));

        // Contract 2: active dispute to test arbitrator resolution and SLA background jobs
        var contract2 = new Contract(
            id: Guid.Parse("44444444-4444-4444-4444-444444444442"),
            title: "AI Analytics Microservices & Dashboard",
            description: "Contract with an active dispute for testing arbitration flows.",
            clientId: clientAlice.Id,
            freelancerId: freelancerElena.Id,
            createdAtUtc: nowUtc.AddDays(-15));
        contract2.Accept();

        var c2m1 = new Milestone(
            id: Guid.Parse("55555555-5555-5555-5555-555555555521"),
            contractId: contract2.Id,
            title: "Predictive Analytics Algorithm Implementation",
            description: "Inference pipeline implementation.",
            amount: 3500.00m,
            dueDateUtc: nowUtc.AddDays(2),
            createdAtUtc: nowUtc.AddDays(-15));
        c2m1.Fund(clientAlice.Id, nowUtc.AddDays(-14));
        c2m1.Submit(freelancerElena.Id, nowUtc.AddDays(-5));

        var dispute1Id = Guid.Parse("66666666-6666-6666-6666-666666666661");
        c2m1.MarkDisputed(dispute1Id, clientAlice.Id, nowUtc.AddDays(-2));

        var dispute1 = new Dispute(
            id: dispute1Id,
            milestoneId: c2m1.Id,
            raisedByUserId: clientAlice.Id,
            reason: "Deliverable latency is higher than the agreed specification.",
            slaExpiresAtUtc: nowUtc.AddDays(2),
            createdAtUtc: nowUtc.AddDays(-2),
            arbitratorId: arbitratorSarah.Id);

        // Contract 3: draft contract ready for freelancer to accept
        var contract3 = new Contract(
            id: Guid.Parse("44444444-4444-4444-4444-444444444443"),
            title: "Cloud Infrastructure & Kubernetes Migration",
            description: "Draft contract created by client, not yet accepted by freelancer.",
            clientId: clientAlice.Id,
            freelancerId: freelancerBob.Id,
            createdAtUtc: nowUtc.AddDays(-1));

        var c3m1 = new Milestone(
            id: Guid.Parse("55555555-5555-5555-5555-555555555531"),
            contractId: contract3.Id,
            title: "Terraform Infrastructure Modules",
            description: "Infrastructure provisioning code.",
            amount: 1800.00m,
            dueDateUtc: nowUtc.AddDays(10),
            createdAtUtc: nowUtc.AddDays(-1));

        // Contract 4: finished contract where all milestones settled
        var contract4 = new Contract(
            id: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            title: "Brand Identity & Design System",
            description: "Completed project to verify the auto-complete contract logic.",
            clientId: clientAlice.Id,
            freelancerId: freelancerElena.Id,
            createdAtUtc: nowUtc.AddDays(-40));
        contract4.Accept();

        var c4m1 = new Milestone(
            id: Guid.Parse("55555555-5555-5555-5555-555555555541"),
            contractId: contract4.Id,
            title: "Brand Guidelines & Vector Assets",
            description: "Brand asset deliverables.",
            amount: 1200.00m,
            dueDateUtc: nowUtc.AddDays(-30),
            createdAtUtc: nowUtc.AddDays(-40));
        c4m1.Fund(clientAlice.Id, nowUtc.AddDays(-39));
        c4m1.Submit(freelancerElena.Id, nowUtc.AddDays(-32));
        c4m1.Approve(clientAlice.Id, nowUtc.AddDays(-30));

        contract4.CompleteIfAllMilestonesSettled();

        await context.Contracts.AddRangeAsync(contract1, contract2, contract3, contract4);
        await context.Milestones.AddRangeAsync(c1m1, c1m2, c1m3, c1m4, c2m1, c3m1, c4m1);
        await context.Disputes.AddAsync(dispute1);

        var ledgerEntries = new List<EscrowLedgerEntry>
        {
            new(Guid.NewGuid(), c1m1.Id, 1500.00m, LedgerEntryType.Fund,
                $"Funded $1,500 for '{c1m1.Title}'", nowUtc.AddDays(-19), clientAlice.Id),
            new(Guid.NewGuid(), c1m1.Id, 1500.00m, LedgerEntryType.Release,
                $"Released $1,500 to Bob for '{c1m1.Title}'", nowUtc.AddDays(-10), clientAlice.Id),

            new(Guid.NewGuid(), c1m2.Id, 2500.00m, LedgerEntryType.Fund,
                $"Funded $2,500 for '{c1m2.Title}'", nowUtc.AddDays(-10), clientAlice.Id),

            new(Guid.NewGuid(), c1m3.Id, 2000.00m, LedgerEntryType.Fund,
                $"Funded $2,000 for '{c1m3.Title}'", nowUtc.AddDays(-2), clientAlice.Id),

            new(Guid.NewGuid(), c2m1.Id, 3500.00m, LedgerEntryType.Fund,
                $"Funded $3,500 for '{c2m1.Title}' (currently in dispute)", nowUtc.AddDays(-14), clientAlice.Id),

            new(Guid.NewGuid(), c4m1.Id, 1200.00m, LedgerEntryType.Fund,
                $"Funded $1,200 for '{c4m1.Title}'", nowUtc.AddDays(-39), clientAlice.Id),
            new(Guid.NewGuid(), c4m1.Id, 1200.00m, LedgerEntryType.Release,
                $"Released $1,200 to Elena for '{c4m1.Title}'", nowUtc.AddDays(-30), clientAlice.Id)
        };

        await context.EscrowLedgerEntries.AddRangeAsync(ledgerEntries);

        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), clientAlice.Id, clientAlice.Email, "CreateContract", "Contract", contract1.Id.ToString(), "Contract created.", true, nowUtc.AddDays(-20)),
            new(Guid.NewGuid(), freelancerBob.Id, freelancerBob.Email, "AcceptContract", "Contract", contract1.Id.ToString(), "Contract accepted.", true, nowUtc.AddDays(-19)),
            new(Guid.NewGuid(), clientAlice.Id, clientAlice.Email, "FundMilestone", "Milestone", c1m1.Id.ToString(), "Milestone funded.", true, nowUtc.AddDays(-19)),
            new(Guid.NewGuid(), freelancerBob.Id, freelancerBob.Email, "SubmitMilestone", "Milestone", c1m1.Id.ToString(), "Deliverable submitted.", true, nowUtc.AddDays(-12)),
            new(Guid.NewGuid(), clientAlice.Id, clientAlice.Email, "ApproveMilestone", "Milestone", c1m1.Id.ToString(), "Milestone approved and released.", true, nowUtc.AddDays(-10)),
            new(Guid.NewGuid(), clientAlice.Id, clientAlice.Email, "RaiseDispute", "Dispute", dispute1.Id.ToString(), "Dispute raised on milestone.", true, nowUtc.AddDays(-2))
        };

        await context.AuditLogs.AddRangeAsync(auditLogs);

        await context.SaveChangesAsync();
        logger.LogInformation("Demo data seeded.");
    }
}
