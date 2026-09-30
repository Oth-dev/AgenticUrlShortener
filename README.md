# Agentic URL Shortener - Assessment Complete Edition

This repository is a .NET 8 take-home prototype designed directly around the supplied
"Agentic-Proficient Software Engineer - URL Shortener" assessment.

## What is implemented

### Working URL shortener
- `POST /api/urls`
- `GET /{code}` redirect
- `GET /api/urls/{code}/analytics`
- `GET /health`

### Agentic SDLC orchestration
- Explicit dependency graph
- Entry and release gates
- Sequential stages
- Parallel implementation + test-design stages
- Synchronization before validation
- Cross-stage context and decision lineage
- Human approval/rejection checkpoint
- Bounded retries
- Fallback
- Rollback
- Safe-stop
- Security/change-control policy guardrails
- Audit events
- Reliability metrics
- Dynamic replanning with parent-run lineage
- Greenfield, brownfield and ambiguous scenarios

## Solution structure

```text
AgenticUrlShortener.sln
src/
  AgenticUrlShortener.Api/
    Controllers/
    Models/
    Repositories/
    Services/
    Orchestration/
tests/
  AgenticUrlShortener.UnitTests/
  AgenticUrlShortener.IntegrationTests/
docs/
  ARCHITECTURE.md
  TESTING_STEPS.md
  SCENARIOS.md
  FINAL_ENGINEERING_SUMMARY.md
  Agentic_AI_Assessment_Guide.pdf
```

## Setup

Requirements:
- .NET 8 SDK
- Visual Studio 2022 17.8+ or VS Code/Rider

Commands:

```bash
dotnet restore AgenticUrlShortener.sln
dotnet build AgenticUrlShortener.sln
dotnet test AgenticUrlShortener.sln
dotnet run --project src/AgenticUrlShortener.Api
```

Open Swagger:
`https://localhost:7168/swagger`

## Important implementation choice

The "agents" are deterministic C# adapters in this take-home prototype. This keeps the
solution runnable without external API keys and makes orchestration behavior deterministic
and testable. The orchestration boundaries are designed so a real LLM/tool adapter can
replace each agent implementation later without changing governance, state, retry,
approval, audit or metrics logic.

## Prototype limitations

- In-memory URL/workflow/metrics stores: restart loses state.
- No real production deployment is performed.
- Rollback invalidates workflow artifacts/state rather than reverting Git/production.
- Metrics are process-local.
- No external LLM is required.
- Security guardrails are illustrative policy rules, not an enterprise policy engine.


## Optional real local LLM (Ollama; no per-request API charges)

This variant adds **actual LLM-generated reasoning** to RequirementAgent and ArchitectureAgent.
Other agents, policy gates, validation, and human approval remain deterministic.

1. Install Ollama from https://ollama.com/download and ensure its local server is running.
2. In a terminal run `ollama pull llama3.2:3b`. The model download needs internet once.
3. Edit `src/AgenticUrlShortener.Api/appsettings.json`: set `Llm.Enabled` to `true`.
4. Start the API and call `POST /api/orchestration/runs` from Swagger.
5. Inspect the requirement/architecture step outputs for `LLM ANALYSIS`.

The default `Enabled: false` keeps CI/tests runnable without an installed model.
Set it to `true` for the LLM interview demo. Ollama itself has no per-request
API charge, but uses your computer's RAM/CPU/GPU and electricity.

**Honest limitations:** No source files are edited by the agents; the
ImplementationAgent produces a proposal, and TestingAgent produces a test plan.
The LLM outputs are not proof that any engineering change was implemented or
verified. This prototype uses a local model without tool execution, authentication,
or production persistence. Do not deploy it as-is.

**Safety:** An unavailable model causes retries and then rollback/safe-stop;
it does not silently pretend an LLM ran. Model text is treated as advisory.
