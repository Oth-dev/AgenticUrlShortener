using Xunit;
using AgenticUrlShortener.Api.Models;
using AgenticUrlShortener.Api.Orchestration;
using AgenticUrlShortener.Api.Repositories;

namespace AgenticUrlShortener.UnitTests;

public sealed class OrchestratorTests
{
    private static AgenticOrchestrator Create() =>
        new(
            new InMemoryWorkflowStore(),
            new InMemoryMetricsStore(),
            new PolicyGuardrail(),
            new WorkflowGraphFactory());

    [Fact]
    public async Task Greenfield_Reaches_Human_Approval()
    {
        var sut = Create();
        var run = await sut.StartAsync(
            "greenfield", "Add expiration dates to short URLs.");

        Assert.Equal(WorkflowStatus.WaitingForApproval, run.Status);
        Assert.All(run.Steps, s => Assert.Equal(StepStatus.Completed, s.Status));
    }

    [Fact]
    public async Task Brownfield_Maps_Existing_Impact()
    {
        var sut = Create();
        var run = await sut.StartAsync(
            "brownfield", "Add last-access time to analytics.");

        var architecture = run.Steps.Single(s => s.Id == "architecture");
        Assert.Contains("Impact map", architecture.Output);
    }

    [Fact]
    public async Task Ambiguous_Preserves_Assumptions_For_Human()
    {
        var sut = Create();
        var run = await sut.StartAsync(
            "ambiguous", "Make the service more secure.");

        Assert.NotEmpty(run.Context.Assumptions);
        Assert.Equal(WorkflowStatus.WaitingForApproval, run.Status);
    }

    [Fact]
    public async Task Graph_Has_Parallel_Branch_And_Join()
    {
        var sut = Create();
        var run = await sut.StartAsync("greenfield", "Add expiry.");

        var implementation = run.Steps.Single(s => s.Id == "implementation");
        var tests = run.Steps.Single(s => s.Id == "test-design");
        var validation = run.Steps.Single(s => s.Id == "validation");

        Assert.Equal(new[] { "architecture" }, implementation.DependsOn);
        Assert.Equal(new[] { "architecture" }, tests.DependsOn);
        Assert.Contains("implementation", validation.DependsOn);
        Assert.Contains("test-design", validation.DependsOn);
    }

    [Fact]
    public async Task Testing_Failure_Uses_Bounded_Retry_Then_Fallback()
    {
        var metrics = new InMemoryMetricsStore();
        var sut = new AgenticOrchestrator(
            new InMemoryWorkflowStore(), metrics,
            new PolicyGuardrail(), new WorkflowGraphFactory());

        var run = await sut.StartAsync(
            "greenfield", "Add expiry. [fail-testing]");

        var test = run.Steps.Single(s => s.Id == "test-design");
        Assert.Equal(2, test.Attempts);
        Assert.Equal(StepStatus.Completed, test.Status);
        Assert.Contains("FALLBACK", test.Output);
        Assert.True(metrics.Snapshot().RetryCount >= 1);
    }

    [Fact]
    public async Task Implementation_Failure_Rolls_Back()
    {
        var sut = Create();
        var run = await sut.StartAsync(
            "greenfield", "Add expiry. [fail-implementation]");

        Assert.Equal(WorkflowStatus.RolledBack, run.Status);
        Assert.Contains(run.AuditTrail, a => a.EventType == "workflow.rollback");
    }

    [Fact]
    public async Task Empty_Requirement_SafeStops_At_Entry_Gate()
    {
        var sut = Create();
        var run = await sut.StartAsync("greenfield", "");

        Assert.Equal(WorkflowStatus.SafeStopped, run.Status);
        Assert.Contains(run.AuditTrail, a => a.EventType == "gate.entry");
    }

    [Fact]
    public async Task Production_AutoDeploy_Is_Blocked_By_Policy()
    {
        var sut = Create();
        var run = await sut.StartAsync(
            "greenfield", "Deploy directly to production.");

        Assert.Equal(WorkflowStatus.SafeStopped, run.Status);
    }

    [Fact]
    public async Task Human_Approval_Completes_Run()
    {
        var sut = Create();
        var run = await sut.StartAsync("greenfield", "Add expiry.");

        var approved = sut.Approve(run.Id, true, "Reviewed.");

        Assert.NotNull(approved);
        Assert.Equal(WorkflowStatus.Completed, approved!.Status);
    }

    [Fact]
    public async Task Replan_Preserves_Parent_Lineage()
    {
        var sut = Create();
        var run = await sut.StartAsync("greenfield", "Add expiry.");

        var replanned = await sut.ReplanAsync(
            run.Id,
            "Add expiry and configurable grace period.",
            "Requirement changed after review.");

        Assert.NotNull(replanned);
        Assert.Equal(run.Id.ToString(), replanned!.Context.Values["parentRunId"]);
        Assert.True(replanned.PlanVersion > run.PlanVersion);
    }
}
