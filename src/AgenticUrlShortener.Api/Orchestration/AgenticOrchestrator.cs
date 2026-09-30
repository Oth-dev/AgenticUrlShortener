using System.Diagnostics;
using AgenticUrlShortener.Api.Models;
using AgenticUrlShortener.Api.Repositories;

namespace AgenticUrlShortener.Api.Orchestration;

public interface IAgenticOrchestrator
{
    Task<WorkflowRun> StartAsync(string scenario, string requirement);
    WorkflowRun? Get(Guid id);
    WorkflowRun? Approve(Guid id, bool approved, string? comment);
    Task<WorkflowRun?> ReplanAsync(Guid id, string updatedRequirement, string reason);
    ReliabilityMetrics Metrics();
}

public sealed class AgenticOrchestrator(
    IWorkflowStore store,
    IMetricsStore metrics,
    IPolicyGuardrail policy,
    IWorkflowGraphFactory graphFactory,
    IAgentExecutor? agentExecutor = null)
    : IAgenticOrchestrator
{
    private const int MaxAttempts = 2;

    public async Task<WorkflowRun> StartAsync(string scenario, string requirement)
    {
        var sw = Stopwatch.StartNew();
        metrics.RunStarted();

        var run = new WorkflowRun
        {
            Scenario = (scenario ?? "").Trim().ToLowerInvariant(),
            Requirement = (requirement ?? "").Trim(),
            Steps = graphFactory.Create(scenario)
        };

        Audit(run, "workflow.started",
            $"Workflow created with plan version {run.PlanVersion}.");

        // ENTRY GATE / POLICY GUARDRAIL
        var entry = policy.EvaluateEntry(run);
        Audit(run, "gate.entry", entry.Reason);

        if (!entry.Allowed)
        {
            run.Status = WorkflowStatus.SafeStopped;
            store.Save(run);
            metrics.RunStopped(sw.Elapsed);
            return run;
        }

        store.Save(run);

        // STATEFUL GRAPH EXECUTION
        // Repeatedly find nodes whose dependencies are complete.
        while (run.Steps.Any(s => s.Status == StepStatus.Pending))
        {
            var ready = run.Steps
                .Where(s => s.Status == StepStatus.Pending &&
                    s.DependsOn.All(d =>
                        run.Steps.Single(x => x.Id == d).Status == StepStatus.Completed))
                .ToList();

            if (ready.Count == 0)
            {
                SafeStop(run, "No executable nodes remain. Dependency graph is blocked.");
                metrics.RunStopped(sw.Elapsed);
                return run;
            }

            // PARALLEL EXECUTION + SYNCHRONIZATION
            // When implementation and test-design become ready together,
            // Task.WhenAll runs them concurrently. The next validation node
            // cannot start until both have completed.
            var results = await Task.WhenAll(
                ready.Select(step => ExecuteStepAsync(run, step)));

            if (results.Any(success => !success))
            {
                // FALLBACK + ROLLBACK
                // First try a bounded fallback for the failed area.
                var recovered = TryFallback(run);

                if (!recovered)
                {
                    RollbackCompletedWork(run,
                        "Fallback could not recover the workflow.");
                    metrics.RunStopped(sw.Elapsed);
                    return run;
                }
            }

            store.Save(run);
        }

        // EXIT / RELEASE GATE
        var release = policy.EvaluateRelease(run);
        Audit(run, "gate.release", release.Reason);

        if (!release.Allowed)
        {
            SafeStop(run, release.Reason);
            metrics.RunStopped(sw.Elapsed);
            return run;
        }

        // CONTROLLED AUTONOMY:
        // Agents cannot complete the workflow. Human approval is mandatory.
        run.Status = WorkflowStatus.WaitingForApproval;
        Audit(run, "approval.required",
            "All automated gates passed. Human approval is required.");

        run.Context.Decisions.Add(
            "Automated execution finished; final quality ownership remains with a human.");

        store.Save(run);
        return run;
    }

    public WorkflowRun? Get(Guid id) => store.Get(id);

    public ReliabilityMetrics Metrics() => metrics.Snapshot();

    public WorkflowRun? Approve(Guid id, bool approved, string? comment)
    {
        var run = store.Get(id);
        if (run is null) return null;

        // CHANGE-CONTROL GATE
        if (run.Status != WorkflowStatus.WaitingForApproval)
        {
            Audit(run, "approval.denied",
                $"Approval ignored because status is {run.Status}.");
            store.Save(run);
            return run;
        }

        run.Status = approved
            ? WorkflowStatus.Completed
            : WorkflowStatus.Rejected;

        run.CompletedAt = DateTimeOffset.UtcNow;

        Audit(run, "approval.decision",
            $"Human decision: {(approved ? "APPROVED" : "REJECTED")}. " +
            $"Comment: {comment ?? "None"}");

        if (approved)
            metrics.RunSucceeded(run.CompletedAt.Value - run.CreatedAt);
        else
            metrics.RunStopped(run.CompletedAt.Value - run.CreatedAt);

        store.Save(run);
        return run;
    }

    // DYNAMIC REPLANNING
    // If the upstream requirement changes, downstream outputs are invalidated,
    // the plan version is incremented, and the graph is executed again.
    public async Task<WorkflowRun?> ReplanAsync(
        Guid id, string updatedRequirement, string reason)
    {
        var run = store.Get(id);
        if (run is null) return null;

        if (run.Status is WorkflowStatus.Completed or WorkflowStatus.Rejected)
            return run; // governance: closed runs are immutable in this prototype

        var old = run.Requirement;

        // Requirement is init-only, so create a new governed run preserving lineage.
        var replanned = new WorkflowRun
        {
            Scenario = run.Scenario,
            Requirement = updatedRequirement.Trim(),
            PlanVersion = run.PlanVersion + 1,
            Steps = graphFactory.Create(run.Scenario)
        };

        replanned.Context.Values["parentRunId"] = run.Id.ToString();
        replanned.Context.Values["previousRequirement"] = old;
        replanned.Context.Decisions.Add(
            $"Replanned because: {reason}");
        Audit(replanned, "workflow.replanned",
            $"Created from run {run.Id}. Reason: {reason}");

        metrics.Replan();

        // Mark old run safe-stopped to preserve immutable lineage.
        run.Status = WorkflowStatus.SafeStopped;
        Audit(run, "workflow.superseded",
            $"Superseded by replanned run {replanned.Id}.");
        store.Save(run);
        store.Save(replanned);

        // Execute the new run through the same governed path.
        // We call StartAsync to keep one execution policy; then copy lineage.
        var executed = await StartAsync(replanned.Scenario, replanned.Requirement);
        executed.PlanVersion = replanned.PlanVersion;
        executed.Context.Values["parentRunId"] = run.Id.ToString();
        executed.Context.Values["previousRequirement"] = old;
        executed.Context.Decisions.Add($"Replanned because: {reason}");
        Audit(executed, "workflow.replan-lineage",
            $"Parent run: {run.Id}");
        store.Save(executed);
        return executed;
    }

    private async Task<bool> ExecuteStepAsync(WorkflowRun run, WorkflowStep step)
    {
        step.StartedAt = DateTimeOffset.UtcNow;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            step.Attempts = attempt;
            step.Status = StepStatus.Running;
            Audit(run, "step.started",
                $"{step.Agent} attempt {attempt} started.", step.Id);

            try
            {
                // Small async yield makes parallel execution visible without
                // pretending there is a long-running external dependency.
                await Task.Yield();

                step.Output = agentExecutor is null
                    ? ExecuteAgent(run, step)
                    : await agentExecutor.ExecuteAsync(run, step,
                        () => ExecuteAgent(run, step));
                step.Status = StepStatus.Completed;
                step.CompletedAt = DateTimeOffset.UtcNow;

                lock (run.Context.Values)
                    run.Context.Values[step.Id] = step.Output;
                Audit(run, "step.completed",
                    $"{step.Agent} completed.", step.Id);
                return true;
            }
            catch (Exception ex)
            {
                Audit(run, "step.failed",
                    $"{step.Agent} attempt {attempt} failed: {ex.Message}", step.Id);

                if (attempt < MaxAttempts)
                {
                    metrics.Retry();
                    Audit(run, "step.retry",
                        $"{step.Agent} will retry.", step.Id);
                    continue;
                }

                step.Status = StepStatus.Failed;
                step.Output = ex.Message;
                step.CompletedAt = DateTimeOffset.UtcNow;
                return false;
            }
        }

        return false;
    }

    private static string ExecuteAgent(WorkflowRun run, WorkflowStep step)
    {
        return step.Agent switch
        {
            "RequirementAgent" => RequirementAgent(run),
            "ArchitectureAgent" => ArchitectureAgent(run),
            "ImplementationAgent" => ImplementationAgent(run),
            "TestingAgent" => TestingAgent(run),
            "ValidationAgent" => ValidationAgent(run),
            "DocumentationAgent" => DocumentationAgent(run),
            "ReleaseReadinessAgent" => ReleaseReadinessAgent(run),
            _ => throw new InvalidOperationException($"Unknown agent {step.Agent}.")
        };
    }

    // ---------------- SPECIALIZED AGENTS ----------------

    private static string RequirementAgent(WorkflowRun run)
    {
        if (run.Scenario == "ambiguous")
        {
            run.Context.Assumptions.Add(
                "Security scope is not fully specified; human confirmation is required.");
            run.Context.Risks.Add(
                "Implementing an unstated security interpretation could solve the wrong problem.");

            return
                "Ambiguity identified. Candidate interpretations include authentication, " +
                "authorization, rate limiting, URL validation, code entropy, expiry, and abuse controls. " +
                "Proceed only with explicit assumptions and preserve them for human review.";
        }

        return
            $"Normalized problem: {run.Requirement}. " +
            "Acceptance: behavior is testable, backward-compatible where applicable, documented, " +
            "and cannot bypass governance gates.";
    }

    private static string ArchitectureAgent(WorkflowRun run)
    {
        if (run.Scenario == "brownfield")
        {
            run.Context.Decisions.Add(
                "Brownfield changes use the smallest compatible change and preserve existing API behavior.");

            return
                "Impact map: ShortUrl model -> repository -> UrlShortenerService -> controllers -> tests. " +
                "Review current API/data flow before modification; preserve compatibility.";
        }

        return
            "Architecture: thin controllers, domain/service layer, repository abstraction, " +
            "explicit orchestration graph, workflow state/context, policy gates, audit events and metrics.";
    }

    private static string ImplementationAgent(WorkflowRun run)
    {
        // Demo hook used to show retry/fallback/rollback.
        if (run.Requirement.Contains("[fail-implementation]",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Intentional implementation failure requested for demonstration.");
        }

        return
            "Implementation artifact proposal: make the smallest cohesive change, keep business logic " +
            "in services, storage behind repositories, validate inputs, and preserve API contracts.";
    }

    private static string TestingAgent(WorkflowRun run)
    {
        if (run.Requirement.Contains("[fail-testing]",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Intentional testing failure requested for demonstration.");
        }

        return
            "Validation plan: unit tests, API integration tests, happy path, invalid input, not-found, " +
            "regression, policy-gate tests, retry/safe-stop tests, and human approval-state tests.";
    }

    private static string ValidationAgent(WorkflowRun run)
    {
        if (!run.Context.Values.ContainsKey("implementation") ||
            !run.Context.Values.ContainsKey("test-design"))
        {
            throw new InvalidOperationException(
                "Synchronization failed: implementation and test-design outputs are both required.");
        }

        return
            "Validation passed: synchronized implementation and test-design outputs are present; " +
            "requirement, architecture and policy context remain traceable.";
    }

    private static string DocumentationAgent(WorkflowRun run) =>
        "Documentation prepared: architecture, setup, APIs, scenarios, testing steps, " +
        "limitations, trade-offs, assumptions and final engineering summary.";

    private static string ReleaseReadinessAgent(WorkflowRun run)
    {
        run.Context.Risks.Add(
            "Prototype stores workflow and URL data in memory; restart loses state.");
        run.Context.Risks.Add(
            "LLM mode is optional and limited to requirement/architecture reasoning; other stages are deterministic proposals, not real source-code edits or executed tests.");

        return
            "Release readiness: automated stages passed. Rollback strategy is state rollback/safe-stop; " +
            "production deployment remains prohibited without human approval.";
    }

    // FALLBACK
    // Demonstrates a bounded alternative path after an agent exhausts retries.
    private bool TryFallback(WorkflowRun run)
    {
        var failed = run.Steps.Where(s => s.Status == StepStatus.Failed).ToList();
        if (failed.Count == 0) return true;

        foreach (var step in failed)
        {
            // Testing fallback: use a conservative manual-validation plan.
            if (step.Agent == "TestingAgent")
            {
                step.Status = StepStatus.Completed;
                step.Output =
                    "FALLBACK: automated test-design agent failed; use mandatory manual test checklist " +
                    "and block release until human review.";
                lock (run.Context.Values)
                    run.Context.Values[step.Id] = step.Output;
                run.Context.Risks.Add(
                    "TestingAgent fallback used; confidence is lower and human validation is mandatory.");
                Audit(run, "step.fallback",
                    "TestingAgent recovered using manual-validation fallback.", step.Id);
                return true;
            }
        }

        return false;
    }

    // ROLLBACK
    // For this prototype there is no real deployment to undo.
    // Rollback means invalidating completed automated work and preserving trace.
    private void RollbackCompletedWork(WorkflowRun run, string reason)
    {
        foreach (var step in run.Steps.Where(s => s.Status == StepStatus.Completed))
            step.Status = StepStatus.RolledBack;

        run.Status = WorkflowStatus.RolledBack;
        run.CompletedAt = DateTimeOffset.UtcNow;
        metrics.Rollback();

        Audit(run, "workflow.rollback", reason);
        store.Save(run);
    }

    private void SafeStop(WorkflowRun run, string reason)
    {
        run.Status = WorkflowStatus.SafeStopped;
        run.CompletedAt = DateTimeOffset.UtcNow;
        Audit(run, "workflow.safe-stop", reason);
        store.Save(run);
    }

    private static void Audit(
        WorkflowRun run, string type, string message, string? stepId = null)
    {
        run.AuditTrail.Add(
            new AuditEvent(DateTimeOffset.UtcNow, type, message, stepId));
    }
}
