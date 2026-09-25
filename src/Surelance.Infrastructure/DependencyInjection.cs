using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Surelance.Application.Common.Interfaces;
using Surelance.Infrastructure.Jobs;
using Surelance.Infrastructure.Outbox;
using Surelance.Infrastructure.Persistence;
using Surelance.Infrastructure.Persistence.Repositories;
using Surelance.Infrastructure.Services;
using System.Text;

namespace Surelance.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"] ?? "SqlServer";
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' is required when DatabaseProvider is SqlServer.");
            }

            connectionString = "Data Source=surelance.db";
        }

        services.AddDbContext<SurelanceDbContext>(options =>
        {
            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString);
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IContractRepository, ContractRepository>();
        services.AddScoped<IMilestoneRepository, MilestoneRepository>();
        services.AddScoped<IEscrowLedgerRepository, EscrowLedgerRepository>();
        services.AddScoped<IDisputeRepository, DisputeRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IMilestoneNotifier, MilestoneNotifier>();

        services.AddScoped<IOutboxProcessor, OutboxProcessor>();

        // Separate database for Hangfire to keep queue polling churn out of the application database
        var hangfireConnectionString = configuration.GetConnectionString("HangfireConnection")
            ?? connectionString;

        services.AddHangfire(config =>
        {
            config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                  .UseSimpleAssemblyNameTypeSerializer()
                  .UseRecommendedSerializerSettings();

            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                EnsureSqlServerDatabaseExists(hangfireConnectionString);

                config.UseSqlServerStorage(hangfireConnectionString, new SqlServerStorageOptions
                {
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true,
                    PrepareSchemaIfNecessary = true
                });
            }
            else
            {
                config.UseMemoryStorage();
            }
        });
        services.AddHangfireServer();
        services.AddScoped<IRecurringJobsService, RecurringJobsService>();

        var secretKey = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is required but not configured in application settings.");
        var issuer = configuration["Jwt:Issuer"] ?? "SurelanceAPI";
        var audience = configuration["Jwt:Audience"] ?? "SurelanceClients";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddHttpContextAccessor();

        return services;
    }

    private static void EnsureSqlServerDatabaseExists(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var dbName = builder.InitialCatalog;
            if (string.IsNullOrWhiteSpace(dbName)) return;

            builder.InitialCatalog = "master";
            using var conn = new SqlConnection(builder.ConnectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = @dbName) CREATE DATABASE [{dbName}]";
            cmd.Parameters.AddWithValue("@dbName", dbName);
            cmd.ExecuteNonQuery();
        }
        catch (SqlException)
        {
            // If master access is restricted, allow Hangfire to proceed with standard connection
        }
    }
}
