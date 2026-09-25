using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Surelance.Application.Common.Behaviors;
using Surelance.Application.Common.Interfaces;
using Surelance.Infrastructure.Services;
using Surelance.Application.Common.Models;
using Surelance.Application.Features.Auth;
using Surelance.Application.Features.Disputes;
using Surelance.Application.Features.Milestones;
using Surelance.Domain.Entities;
using Surelance.Domain.Enums;
using Xunit;

namespace Surelance.UnitTests;

public class ValidationAndAuditTests
{
    [Theory]
    [InlineData("", 100, false)]
    [InlineData("Valid Title", 0, false)]
    [InlineData("Valid Title", -50, false)]
    [InlineData("Valid Title", 500, true)]
    public void CreateMilestoneCommandValidator_ShouldValidateCorrectly(string title, decimal amount, bool expectedValid)
    {
        var validator = new CreateMilestoneCommandValidator();
        var command = new CreateMilestoneCommand(Guid.NewGuid(), title, "Description", amount, null);

        var result = validator.Validate(command);

        result.IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData("invalid-email", "pass123", false)]
    [InlineData("user@test.com", "123", false)]
    [InlineData("user@test.com", "pass123", true)]
    public void RegisterCommandValidator_ShouldValidateCorrectly(string email, string password, bool expectedValid)
    {
        var validator = new RegisterCommandValidator();
        var command = new RegisterCommand(email, password, "Full Name", UserRole.Client);

        var result = validator.Validate(command);

        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void ResolveDisputeCommandValidator_WhenResolutionIsNone_ShouldBeInvalid()
    {
        var validator = new ResolveDisputeCommandValidator();
        var command = new ResolveDisputeCommand(Guid.NewGuid(), DisputeResolution.None, "Notes");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Resolution");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-10, false)]
    [InlineData(169, false)]
    [InlineData(24, true)]
    [InlineData(168, true)]
    public void ExtendDisputeSlaCommandValidator_ShouldValidateCorrectly(int additionalHours, bool expectedValid)
    {
        var validator = new ExtendDisputeSlaCommandValidator();
        var command = new ExtendDisputeSlaCommand(Guid.NewGuid(), additionalHours);

        var result = validator.Validate(command);

        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public async Task AuditLoggingBehavior_WhenCommandExecutes_ShouldPersistAuditRecord()
    {
        var auditRepoMock = new Mock<IAuditLogRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var dateTimeMock = new Mock<IDateTimeProvider>();
        var loggerMock = new Mock<ILogger<AuditLoggingBehavior<FundMilestoneCommand, Result>>>();

        var userId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var now = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

        currentUserMock.Setup(u => u.UserId).Returns(userId);
        currentUserMock.Setup(u => u.Email).Returns("client@test.com");
        dateTimeMock.Setup(d => d.UtcNow).Returns(now);
        unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var behavior = new AuditLoggingBehavior<FundMilestoneCommand, Result>(
            auditRepoMock.Object,
            unitOfWorkMock.Object,
            currentUserMock.Object,
            dateTimeMock.Object,
            loggerMock.Object);

        var command = new FundMilestoneCommand(milestoneId);

        var result = await behavior.Handle(command, (ct) => Task.FromResult(Result.Success()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        auditRepoMock.Verify(a => a.AddAsync(
            It.Is<AuditLog>(log => 
                log.UserId == userId && 
                log.Action == nameof(FundMilestoneCommand) && 
                log.EntityName == "Milestone" && 
                log.EntityId == milestoneId.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void JwtTokenGenerator_WhenSecretKeyMissing_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder().Build();
        var generator = new JwtTokenGenerator(config);
        var user = new User(Guid.NewGuid(), "test@user.com", "hash", "Test User", UserRole.Client, DateTime.UtcNow);

        var act = () => generator.GenerateToken(user);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:SecretKey is required*");
    }

    [Fact]
    public void JwtTokenGenerator_WhenSecretKeyProvided_GeneratesValidJwtToken()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "SuperSecretKeyForTestingThatIsLongEnough12345678!"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var generator = new JwtTokenGenerator(config);
        var user = new User(Guid.NewGuid(), "client@test.com", "hash", "Client Alice", UserRole.Client, DateTime.UtcNow);

        var token = generator.GenerateToken(user);

        token.Should().NotBeNullOrWhiteSpace();
        token.Split('.').Should().HaveCount(3);
    }
}
