using AgenticUrlShortener.Api.Models;

namespace AgenticUrlShortener.Api.Orchestration;

public interface IWorkflowGraphFactory
{
    List<WorkflowStep> Create(string scenario);
}

// EXPLICIT DEPENDENCY GRAPH
//
// requirement
//     |
//     v
// architecture
//   /      \
//  v        v
// implementation   test-design       <-- parallel branch
//   \        /
//    v      v
//   validation                     <-- synchronization/join
//      |
//      v
// documentation
//      |
//      v
// release-readiness
//      |
//      v
// human approval
//
// Each node has DependsOn. The orchestrator executes only nodes whose
// dependencies are completed. That makes dependencies explicit rather than
// hard-coding a simple linear chain.
public sealed class WorkflowGraphFactory : IWorkflowGraphFactory
{
    public List<WorkflowStep> Create(string scenario)
    {
        return new()
        {
            Step("requirement", "RequirementAgent",
                "Normalize intent, ambiguity, assumptions and acceptance criteria"),

            Step("architecture", "ArchitectureAgent",
                "Design impacted components, APIs, data flow and key decisions",
                "requirement"),

            // These two depend only on architecture, so the orchestrator
            // can run them in parallel.
            Step("implementation", "ImplementationAgent",
                "Generate an implementation/change proposal",
                "architecture"),

            Step("test-design", "TestingAgent",
                "Generate validation, unit/integration and failure-path tests",
                "architecture"),

            // Synchronization point: validation waits for BOTH parallel nodes.
            Step("validation", "ValidationAgent",
                "Validate implementation against requirement, tests and policies",
                "implementation", "test-design"),

            Step("documentation", "DocumentationAgent",
                "Produce setup, API, scenario and decision documentation",
                "validation"),

            Step("release", "ReleaseReadinessAgent",
                "Check risks, rollback, observability and release readiness",
                "documentation")
        };
    }

    private static WorkflowStep Step(
        string id, string agent, string purpose, params string[] deps) =>
        new()
        {
            Id = id,
            Agent = agent,
            Purpose = purpose,
            DependsOn = deps.ToList()
        };
}
