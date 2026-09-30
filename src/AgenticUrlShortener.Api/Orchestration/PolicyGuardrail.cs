using AgenticUrlShortener.Api.Models;

namespace AgenticUrlShortener.Api.Orchestration;

public sealed record PolicyDecision(bool Allowed, string Reason);

public interface IPolicyGuardrail
{
    PolicyDecision EvaluateEntry(WorkflowRun run);
    PolicyDecision EvaluateRelease(WorkflowRun run);
}

// Prototype governance policy.
// Production would externalize these rules into configuration/policy-as-code.
public sealed class PolicyGuardrail : IPolicyGuardrail
{
    public PolicyDecision EvaluateEntry(WorkflowRun run)
    {
        if (string.IsNullOrWhiteSpace(run.Requirement))
            return new(false, "Requirement cannot be empty.");

        var allowed = new[] { "greenfield", "brownfield", "ambiguous" };
        if (!allowed.Contains(run.Scenario, StringComparer.OrdinalIgnoreCase))
            return new(false, "Scenario must be greenfield, brownfield, or ambiguous.");

        // Security/change-control guardrail:
        // this prototype never allows an agent to auto-deploy.
        if (run.Requirement.Contains("deploy directly to production",
            StringComparison.OrdinalIgnoreCase))
        {
            return new(false,
                "Policy blocks autonomous production deployment.");
        }

        return new(true, "Entry policy passed.");
    }

    public PolicyDecision EvaluateRelease(WorkflowRun run)
    {
        if (run.Steps.Any(s => s.Status != StepStatus.Completed))
            return new(false, "All workflow steps must complete before approval.");

        // Ambiguous requirements may be analyzed, but must preserve the
        // uncertainty for human review instead of silently inventing intent.
        if (run.Scenario.Equals("ambiguous", StringComparison.OrdinalIgnoreCase) &&
            run.Context.Assumptions.Count == 0)
        {
            return new(false, "Ambiguous scenario requires explicit assumptions.");
        }

        return new(true, "Release gate passed; human approval still required.");
    }
}
