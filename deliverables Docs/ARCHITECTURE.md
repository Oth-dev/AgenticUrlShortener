# Architecture Overview

## Components

1. **URL API**
   - `UrlsController`
   - `RedirectController`
   - `UrlShortenerService`
   - `IUrlRepository`

2. **Agentic Orchestration API**
   - `OrchestrationController`
   - `AgenticOrchestrator`
   - `WorkflowGraphFactory`
   - `PolicyGuardrail`
   - `IWorkflowStore`
   - `IMetricsStore`

3. **Workflow state**
   - `WorkflowRun`
   - `WorkflowStep`
   - `WorkflowContext`
   - `AuditEvent`

## Explicit dependency graph

```text
requirement
    |
architecture
   / \
  /   \
implementation   test-design
  \   /
   \ /
validation
    |
documentation
    |
release
    |
HUMAN APPROVAL
```

`implementation` and `test-design` execute in parallel using `Task.WhenAll`.
`validation` is a synchronization point and cannot run until both are completed.

## Entry gate

Before any agent runs:
- requirement must not be empty
- scenario must be supported
- autonomous production deployment is blocked

## Exit gate

Before human approval:
- every automated graph node must complete
- ambiguous requirements must preserve explicit assumptions

## Context and lineage

Each step output is copied into `WorkflowContext.Values`.
Decisions, risks and assumptions are kept separately.
Replanning stores `parentRunId` and the previous requirement.

## Failure control

- Maximum two attempts per step
- Retry is counted in metrics
- TestingAgent has a conservative manual-validation fallback
- Unrecoverable implementation failure triggers rollback
- Blocked/invalid graphs safe-stop
- Closed runs cannot be dynamically replanned

## Controlled autonomy

The automated workflow can reach only `WaitingForApproval`.
Only the human approval endpoint can set `Completed`.

## Reliability/observability

`GET /api/orchestration/metrics` exposes:
- total runs
- successful runs
- stopped runs
- retry count
- rollback count
- replan count
- success rate
- average latency
- average recovery time

Each run also contains an audit event stream.
