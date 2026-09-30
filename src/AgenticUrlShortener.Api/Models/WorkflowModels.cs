namespace AgenticUrlShortener.Api.Models;

// Overall lifecycle status.
public enum WorkflowStatus
{
    Running,
    WaitingForApproval,
    Completed,
    Rejected,
    SafeStopped,
    RolledBack
}

public enum StepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped,
    RolledBack
}

// A node in the explicit dependency graph.
public sealed class WorkflowStep
{
    public required string Id { get; init; }
    public required string Agent { get; init; }
    public required string Purpose { get; init; }

    // IDs of steps that must complete first.
    public List<string> DependsOn { get; init; } = new();

    public StepStatus Status { get; set; } = StepStatus.Pending;
    public int Attempts { get; set; }
    public string Output { get; set; } = "";
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

// Cross-stage context preserves decisions and outputs.
// Later agents read this instead of losing upstream reasoning.
public sealed class WorkflowContext
{
    public Dictionary<string, string> Values { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<string> Decisions { get; init; } = new();
    public List<string> Risks { get; init; } = new();
    public List<string> Assumptions { get; init; } = new();
}

// One run = one engineering requirement.
public sealed class WorkflowRun
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Scenario { get; init; }
    public required string Requirement { get; init; }

    public WorkflowStatus Status { get; set; } = WorkflowStatus.Running;

    // Incremented whenever dynamic replanning changes the execution plan.
    public int PlanVersion { get; set; } = 1;

    public List<WorkflowStep> Steps { get; init; } = new();
    public WorkflowContext Context { get; init; } = new();

    // Audit-grade-for-prototype trace: every state transition is recorded.
    public List<AuditEvent> AuditTrail { get; init; } = new();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed record AuditEvent(
    DateTimeOffset Timestamp,
    string EventType,
    string Message,
    string? StepId = null);

// Aggregate reliability metrics required by the assignment.
public sealed class ReliabilityMetrics
{
    public long TotalRuns { get; set; }
    public long SuccessfulRuns { get; set; }
    public long FailedOrStoppedRuns { get; set; }
    public long RetryCount { get; set; }
    public long RollbackCount { get; set; }
    public long ReplanCount { get; set; }
    public double AverageLatencyMs { get; set; }
    public double AverageRecoveryTimeMs { get; set; }

    public double SuccessRate =>
        TotalRuns == 0 ? 0 : (double)SuccessfulRuns / TotalRuns;
}
