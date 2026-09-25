using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Surelance.Application.Common.Interfaces;
using Surelance.Domain.Entities;
using Surelance.Domain.Enums;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Surelance.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is required but not configured in application settings.");
        var issuer = _configuration["Jwt:Issuer"] ?? "SurelanceAPI";
        var audience = _configuration["Jwt:Audience"] ?? "SurelanceClients";
        var expirationMinutes = int.TryParse(_configuration["Jwt:ExpirationMinutes"], out var mins) ? mins : 120;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var idClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
    }

    public string? Email =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

    public UserRole? Role
    {
        get
        {
            var roleClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;
            return Enum.TryParse<UserRole>(roleClaim, true, out var role) ? role : null;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public class MilestoneNotifier : IMilestoneNotifier
{
    private readonly ILogger<MilestoneNotifier> _logger;

    public MilestoneNotifier(ILogger<MilestoneNotifier> logger)
    {
        _logger = logger;
    }

    public Task NotifyMilestoneFundedAsync(Guid milestoneId, Guid contractId, decimal amount, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("NOTIFY: Milestone '{MilestoneId}' on contract '{ContractId}' was funded with {Amount:C}. Freelancer notified.",
            milestoneId, contractId, amount);
        return Task.CompletedTask;
    }

    public Task NotifyMilestoneSubmittedAsync(Guid milestoneId, Guid contractId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("NOTIFY: Milestone '{MilestoneId}' on contract '{ContractId}' was submitted for review. Client notified.",
            milestoneId, contractId);
        return Task.CompletedTask;
    }

    public Task NotifyMilestoneApprovedAsync(Guid milestoneId, Guid contractId, decimal amount, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("NOTIFY: Milestone '{MilestoneId}' on contract '{ContractId}' was approved. Released {Amount:C} to Freelancer.",
            milestoneId, contractId, amount);
        return Task.CompletedTask;
    }

    public Task NotifyDisputeRaisedAsync(Guid milestoneId, Guid disputeId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("NOTIFY: Dispute '{DisputeId}' raised on Milestone '{MilestoneId}'. Arbitrator and counterpart notified.",
            disputeId, milestoneId);
        return Task.CompletedTask;
    }

    public Task NotifyDisputeResolvedAsync(Guid disputeId, Guid milestoneId, DisputeResolution resolution, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("NOTIFY: Dispute '{DisputeId}' on Milestone '{MilestoneId}' was resolved ({Resolution}). All parties notified.",
            disputeId, milestoneId, resolution);
        return Task.CompletedTask;
    }

    public Task SendDeadlineReminderAsync(Guid milestoneId, string milestoneTitle, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("REMINDER: Milestone '{MilestoneId}' ('{Title}') deadline is near or has elapsed!",
            milestoneId, milestoneTitle);
        return Task.CompletedTask;
    }
}
