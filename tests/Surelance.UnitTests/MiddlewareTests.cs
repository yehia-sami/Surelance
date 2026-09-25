using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Surelance.API.Middleware;
using System.Text.Json;
using Xunit;

namespace Surelance.UnitTests;

public class MiddlewareTests
{
    [Fact]
    public async Task GlobalExceptionHandlingMiddleware_WhenDbUpdateConcurrencyExceptionThrown_Returns409ConflictWithProblemDetails()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/milestones/some-id/approve";
        context.Response.Body = new MemoryStream();

        var loggerMock = new Mock<ILogger<GlobalExceptionHandlingMiddleware>>();
        var environmentMock = new Mock<IHostEnvironment>();
        environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        RequestDelegate next = _ => throw new DbUpdateConcurrencyException("Concurrency conflict occurred during save");

        var middleware = new GlobalExceptionHandlingMiddleware(next, loggerMock.Object, environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        context.Response.ContentType.Should().Be("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(StatusCodes.Status409Conflict);
        problemDetails.Title.Should().Be("The resource was modified by another request");
        problemDetails.Detail.Should().Contain("reload the resource and retry");
        problemDetails.Instance.Should().Be("/api/v1/milestones/some-id/approve");

        // Verify logged at Warning level
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
