namespace AgenticUrlShortener.Api.Contracts;

public sealed record CreateUrlRequest(string Url);

// Scenario values used by the demo:
// greenfield, brownfield, ambiguous
public sealed record StartWorkflowRequest(string Scenario, string Requirement);

public sealed record ApprovalRequest(bool Approved, string? Comment);

// Demonstrates that an upstream requirement changed after a run started.
// This triggers governed dynamic replanning.
public sealed record ReplanRequest(string UpdatedRequirement, string Reason);
