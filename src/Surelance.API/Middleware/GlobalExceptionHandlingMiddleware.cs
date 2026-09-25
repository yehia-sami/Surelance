using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace Surelance.API.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "A concurrency conflict occurred while processing request {Path}: {Message}", context.Request.Path, ex.Message);
            await HandleConcurrencyExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleConcurrencyExceptionAsync(HttpContext context, DbUpdateConcurrencyException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.Conflict;

        var detail = _environment.IsDevelopment()
            ? $"The resource was modified by another request. Please reload the resource and retry. {exception.Message}"
            : "The resource was modified by another request. Please reload the resource and retry.";

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "The resource was modified by another request",
            Detail = detail,
            Instance = context.Request.Path
        };

        var responseJson = JsonSerializer.Serialize(problemDetails);
        await context.Response.WriteAsync(responseJson);
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var detail = _environment.IsDevelopment()
            ? exception.Message
            : "An unexpected server error occurred. Please try again later.";

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected server error occurred.",
            Detail = detail,
            Instance = context.Request.Path
        };

        var responseJson = JsonSerializer.Serialize(problemDetails);
        await context.Response.WriteAsync(responseJson);
    }
}
