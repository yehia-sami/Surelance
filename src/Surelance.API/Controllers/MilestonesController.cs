using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Surelance.Application.Features.Disputes;
using Surelance.Application.Features.Milestones;

namespace Surelance.API.Controllers;

[Authorize]
public class MilestonesController : ApiControllerBase
{
    [HttpPost("{id:guid}/fund")]
    [Authorize(Roles = "Client")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> FundMilestone([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new FundMilestoneCommand(id));
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "Freelancer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SubmitMilestone([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new SubmitMilestoneCommand(id));
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Client")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ApproveMilestone([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new ApproveMilestoneCommand(id));
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/dispute")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RaiseDispute([FromRoute] Guid id, [FromBody] RaiseDisputeRequest request)
    {
        var result = await Mediator.Send(new RaiseDisputeCommand(id, request.Reason));
        return HandleResult(result);
    }

    [HttpGet("{id:guid}/ledger")]
    [ProducesResponseType(typeof(MilestoneLedgerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMilestoneLedger([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetMilestoneLedgerQuery(id));
        return HandleResult(result);
    }

    [HttpPost("deadlines/check")]
    [Authorize(Roles = "Arbitrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CheckMilestoneDeadlines([FromServices] Surelance.Infrastructure.Jobs.IRecurringJobsService recurringJobsService)
    {
        await recurringJobsService.CheckMilestoneDeadlinesAsync();
        return Ok(new { success = true });
    }
}

public record RaiseDisputeRequest(string Reason);
