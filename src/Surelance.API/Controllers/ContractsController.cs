using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Surelance.Application.Features.Contracts;
using Surelance.Application.Features.Milestones;

namespace Surelance.API.Controllers;

[Authorize]
public class ContractsController : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Client")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateContract([FromBody] CreateContractCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = "Freelancer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AcceptContract([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new AcceptContractCommand(id));
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = "Client")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CloseContract([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new CloseContractCommand(id));
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/milestones")]
    [Authorize(Roles = "Client")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddMilestone([FromRoute] Guid id, [FromBody] AddMilestoneRequest request)
    {
        var command = new CreateMilestoneCommand(id, request.Title, request.Description, request.Amount, request.DueDateUtc);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContractDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetContractById([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetContractByIdQuery(id));
        return HandleResult(result);
    }

    [HttpGet("my")]
    [ProducesResponseType(typeof(IReadOnlyList<ContractSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyContracts()
    {
        var result = await Mediator.Send(new GetMyContractsQuery());
        return HandleResult(result);
    }
}

public record AddMilestoneRequest(
    string Title,
    string Description,
    decimal Amount,
    DateTime? DueDateUtc);
