# Surelance

A freelance escrow and milestone payment tracker I built with .NET 8. 

The core idea is simple: instead of clients paying freelancers upfront or freelancers working on promise, clients fund milestones into an escrow ledger. Money is locked until the client approves the work. If there's an issue, either person can open a dispute which brings in an arbitrator or triggers an automated resolution if the dispute sits unattended.

## Why I Built This

I wanted a portfolio project that went deeper than standard CRUD tutorials (todo apps, basic ecommerce carts, etc.). Specifically, I wanted hands-on practice with:

- **Clean Architecture & CQRS**: structuring code into Domain, Application, Infrastructure, and API layers using MediatR vertical slices.
- **Append-Only Financial Ledgers**: modelling escrow as an immutable transaction log (`Fund`, `Release`, `Refund`) rather than mutating a raw balance column back and forth.
- **State Machine Boundaries**: writing domain entities that strictly guard their own transitions (e.g., you can't submit a milestone that hasn't been funded, and once it's released or refunded it's permanently locked).
- **Background Jobs & Eventual Consistency**: using Hangfire and an Outbox pattern for background tasks like auto-resolving expired disputes and firing notifications.

## How to Run

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server LocalDB (installed by default with Visual Studio) or a standard SQL Server instance

### Configuration & Secrets
Sensitive credentials like the JWT signing key are managed outside of source control via .NET User Secrets. Before running locally, set the secret key:

```bash
dotnet user-secrets set "Jwt:SecretKey" "SurelanceSuperSecretEscrowKeyForDevelopment2026!MustBe32BytesOrMore" --project src/Surelance.API
```

### Running the API
From the repository root:

```bash
dotnet run --project src/Surelance.API
```

Once running:
- **Swagger UI**: available at `http://localhost:5000`
- **Hangfire Dashboard**: live at `http://localhost:5000/hangfire`

> **Note on HTTPS**: Only HTTP (port 5000) is bound locally—there is no `https://localhost:5001`. `UseHttpsRedirection()` is intentionally skipped in Development (`if (!app.Environment.IsDevelopment())`) to avoid a confusing "Failed to determine the https port for redirect" warning. HTTPS redirection re-enables automatically outside Development, where a reverse proxy or production certificate terminates TLS.

### Running without SQL Server
To try the API without SQL Server, switch the provider to SQLite (the schema is created from the model; Hangfire uses in-memory storage):

```bash
dotnet run --project src/Surelance.API --DatabaseProvider=Sqlite --ConnectionStrings:DefaultConnection="Data Source=surelance.db"
```

Optimistic concurrency (`RowVersion`) is only enforced on SQL Server.

### Running the Tests
```bash
dotnet test
```

All 46 unit tests run against in-memory mocks and SQLite, covering the workflow rules, SLA resolution, validation pipeline behaviors, background job and outbox processing, and seeding a fresh database.

## Demo Accounts

When the app boots, `DbInitializer` seeds four test accounts with a few realistic contract states:

| Role | Email | Password | Notes |
|---|---|---|---|
| **Client** | `client@surelance.com` | `Client@123` | Alice — has active and completed contracts |
| **Freelancer** | `freelancer@surelance.com` | `Freelancer@123` | Bob — has milestones in progress and submitted |
| **Freelancer** | `elena@surelance.com` | `Freelancer@123` | Elena — has an open disputed milestone and a finished brand project |
| **Arbitrator** | `arbitrator@surelance.com` | `Arbitrator@123` | Sarah — can review and resolve open disputes |

There's also a `Surelance.API.http` file included in the API project if you want to click through the endpoints directly in VS Code / Visual Studio.

## Notes & Tradeoffs

A few things wort-h mentioning about how things are set up and what I'd improve:

- **Migrations vs EnsureCreated**: Early on while prototyping the domain entities, I used `EnsureCreatedAsync` in `DbInitializer` just to avoid recreating migrations every time I tweaked a property. Once the entities and ledger models stabilized, I added proper EF Core migrations (`dotnet-ef`) and switched the startup check to `MigrateAsync`.
- **Dual Database Setup**: I split Hangfire into its own database (`Surelance_HangfireDb`) instead of letting its queue tables live alongside the domain tables. In a real environment Hangfire creates quite a bit of polling and table churn, so keeping the domain database clean felt worth the extra connection string.
- **CORS Allowed Origins**: The API CORS policy is restricted to origins configured in `AllowedOrigins` inside `appsettings.json` (defaults to `["http://localhost:3000"]` for local frontend development) rather than allowing all origins.
- **Hangfire Dashboard Auth**: Right now in development mode, loopback/localhost requests are allowed without credentials so the dashboard is easy to inspect while running locally. Remote requests require an authenticated token with the `Arbitrator` role.
