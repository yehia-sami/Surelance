using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Surelance.Application.Features.Disputes;
using Surelance.Domain.Enums;

namespace Surelance.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DisputesController : ApiControllerBase
{
    /// <summary>
    /// Resolves an open dispute. Restricted to Arbitrators (RefundClient or ReleaseFreelancer).
    /// </summary>
    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = "Arbitrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResolveDispute([FromRoute] Guid id, [FromBody] ResolveDisputeRequest request)
    {
        var command = new ResolveDisputeCommand(id, request.Resolution, request.Notes);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DisputeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDisputeById([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetDisputeByIdQuery(id));
        return HandleResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DisputeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDisputes()
    {
        var result = await Mediator.Send(new GetDisputesQuery());
        return HandleResult(result);
    }

    /// <summary>
    /// Evaluates expired disputes against the 48-hour SLA and applies the default resolution based on who raised it.
    /// </summary>
    [HttpPost("sla/check")]
    [Authorize(Roles = "Arbitrator")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CheckDisputeSla()
    {
        var result = await Mediator.Send(new CheckDisputeSlaCommand());
        return HandleResult(result);
    }

    /// <summary>
    /// Extends the SLA deadline for an open dispute. Restricted to Arbitrators.
    /// </summary>
    [HttpPost("{id:guid}/extend-sla")]
    [Authorize(Roles = "Arbitrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExtendDisputeSla([FromRoute] Guid id, [FromBody] ExtendDisputeSlaRequest request)
    {
        var command = new ExtendDisputeSlaCommand(id, request.AdditionalHours);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}

public record ResolveDisputeRequest(
    DisputeResolution Resolution,
    string Notes);

public record ExtendDisputeSlaRequest(int AdditionalHours);
