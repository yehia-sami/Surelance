using MediatR;
using Microsoft.AspNetCore.Mvc;
using Surelance.Application.Common.Models;

namespace Surelance.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected IActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
        {
            return Ok(new { success = true });
        }

        return ToProblemDetailsResult(result.ErrorType, result.ErrorMessage, result.Errors);
    }

    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return ToProblemDetailsResult(result.ErrorType, result.ErrorMessage, result.Errors);
    }

    private IActionResult ToProblemDetailsResult(ErrorType errorType, string? errorMessage, IReadOnlyCollection<string> errors)
    {
        var (statusCode, title) = errorType switch
        {
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Resource Not Found"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status400BadRequest, "Validation Error")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = errorMessage,
            Extensions = { ["errors"] = errors }
        };

        return errorType switch
        {
            ErrorType.NotFound => NotFound(problemDetails),
            ErrorType.Forbidden => new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status403Forbidden },
            ErrorType.Conflict => Conflict(problemDetails),
            _ => BadRequest(problemDetails)
        };
    }
}
