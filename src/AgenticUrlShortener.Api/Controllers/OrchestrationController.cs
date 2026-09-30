using AgenticUrlShortener.Api.Contracts;
using AgenticUrlShortener.Api.Orchestration;
using Microsoft.AspNetCore.Mvc;

namespace AgenticUrlShortener.Api.Controllers;

[ApiController]
[Route("api/orchestration")]
public sealed class OrchestrationController(IAgenticOrchestrator orchestrator)
    : ControllerBase
{
    // Start an end-to-end governed agentic run.
    [HttpPost("runs")]
    public async Task<IActionResult> Start(StartWorkflowRequest request) =>
        Ok(await orchestrator.StartAsync(request.Scenario, request.Requirement));

    // Inspect state, graph nodes, context/lineage and audit events.
    [HttpGet("runs/{id:guid}")]
    public IActionResult Get(Guid id)
    {
        var run = orchestrator.Get(id);
        return run is null ? NotFound() : Ok(run);
    }

    // Human-in-the-loop approval/rejection gate.
    [HttpPost("runs/{id:guid}/approve")]
    public IActionResult Approve(Guid id, ApprovalRequest request)
    {
        var run = orchestrator.Approve(id, request.Approved, request.Comment);
        return run is null ? NotFound() : Ok(run);
    }

    // Demonstrates dynamic replanning when upstream intent changes.
    [HttpPost("runs/{id:guid}/replan")]
    public async Task<IActionResult> Replan(Guid id, ReplanRequest request)
    {
        var run = await orchestrator.ReplanAsync(
            id, request.UpdatedRequirement, request.Reason);

        return run is null ? NotFound() : Ok(run);
    }

    // Reliability/observability endpoint.
    [HttpGet("metrics")]
    public IActionResult Metrics() => Ok(orchestrator.Metrics());
}
