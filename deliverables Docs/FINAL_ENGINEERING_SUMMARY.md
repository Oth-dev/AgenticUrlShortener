# Final Engineering Summary

## Plan and rationale

The solution separates the working URL-shortener domain from the agentic SDLC orchestration
layer. The orchestrator uses an explicit dependency graph rather than a simple hard-coded
linear chain. Governance remains outside specialized agents so retry, policy, audit,
approval and state-transition rules are consistent.

## Artifacts

- .NET 8 Web API
- URL shortening + redirect + analytics
- Agentic orchestrator
- Dependency graph
- Policy gates
- Audit trail
- Metrics
- Dynamic replanning
- Unit tests
- API integration tests
- Swagger
- Architecture/setup/testing/scenario documentation
- Beginner learning PDF

## Key decisions

1. Deterministic agents instead of an external LLM:
   keeps the prototype runnable, testable and free of API-key dependencies.
2. In-memory stores:
   optimize take-home setup; interfaces preserve a path to durable storage.
3. Human approval after release-readiness:
   prevents autonomous completion of a high-impact change.
4. Explicit graph:
   makes dependencies, parallelism and synchronization visible.
5. Fallback only where safe:
   testing may fall back to mandatory manual validation; implementation failure rolls back.

## Risks and trade-offs

- In-memory state is not durable or distributed.
- Deterministic agents demonstrate orchestration but not model reasoning quality.
- Rollback is workflow-artifact rollback, not Git/deployment rollback.
- Metrics are process-local and illustrative.
- Policy rules are code-based rather than enterprise policy-as-code.
- No authentication is added to the demo approval endpoint; production requires identity,
  authorization and immutable audit storage.

## Validation

- Unit tests cover orchestration rules and domain behavior.
- Integration tests exercise HTTP endpoints.
- Swagger steps demonstrate all required scenarios and failure controls.
- Human approval remains the final quality gate.

## Assumptions

- The assessment values orchestration behavior independently of a specific LLM vendor.
- No real production deployment is expected from a take-home prototype.
- External infrastructure should not be required to run the demonstration.

## Production next steps

- Durable SQL/PostgreSQL workflow store
- distributed locking/idempotency
- real LLM/tool adapters with structured outputs
- authentication and policy-based authorization
- immutable centralized audit/logging
- OpenTelemetry traces and production metrics
- Git branch/PR integration
- sandboxed code execution
- real test runner/tool invocation
- deployment integration with environment-specific approval gates
