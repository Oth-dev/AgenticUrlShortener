# Testing Steps

## A. Build and automated tests

From the solution folder:

```bash
dotnet restore AgenticUrlShortener.sln
dotnet build AgenticUrlShortener.sln
dotnet test AgenticUrlShortener.sln
```

Unit tests cover:
- URL validation and analytics
- greenfield
- brownfield
- ambiguous requirement handling
- explicit graph dependencies
- parallel branch/join definition
- entry guardrails
- retry + fallback
- rollback
- human approval
- dynamic replanning/lineage

Integration tests cover:
- health endpoint
- URL creation
- end-to-end greenfield orchestration API
- metrics endpoint

## B. Swagger manual test

Run:

```bash
dotnet run --project src/AgenticUrlShortener.Api
```

Open:
`https://localhost:7168/swagger`

### 1. Health
`GET /health`

Expected: `200`.

### 2. Create URL
`POST /api/urls`

```json
{
  "url": "https://www.google.com"
}
```

Copy `code` and `shortUrl`.

### 3. Redirect
Open `shortUrl` directly in a browser tab.
Do not rely on Swagger for an external redirect.

### 4. Analytics
`GET /api/urls/{code}/analytics`

Use the real generated code. Re-open the short URL and confirm `clicks` increases.

### 5. Greenfield
`POST /api/orchestration/runs`

```json
{
  "scenario": "greenfield",
  "requirement": "Add expiration dates to short URLs."
}
```

Verify:
- `requirement` completed
- `architecture` completed
- `implementation` and `test-design` both depend on architecture
- `validation` depends on both parallel nodes
- `documentation` completed
- `release` completed
- status is `WaitingForApproval`

### 6. Inspect run
`GET /api/orchestration/runs/{id}`

Review:
- steps
- dependency IDs
- attempts
- context values
- decisions
- risks
- assumptions
- audit trail

### 7. Approve
`POST /api/orchestration/runs/{id}/approve`

```json
{
  "approved": true,
  "comment": "Reviewed architecture, validation and risks."
}
```

Expected: `Completed`.

### 8. Brownfield
`POST /api/orchestration/runs`

```json
{
  "scenario": "brownfield",
  "requirement": "Update existing analytics to include the last access time."
}
```

Inspect `ArchitectureAgent` output for impacted modules and compatibility.

### 9. Ambiguous
`POST /api/orchestration/runs`

```json
{
  "scenario": "ambiguous",
  "requirement": "Make the URL shortener more secure."
}
```

Verify `Context.Assumptions` and `Context.Risks` are populated.

### 10. Retry + fallback
`POST /api/orchestration/runs`

```json
{
  "scenario": "greenfield",
  "requirement": "Add expiration dates. [fail-testing]"
}
```

Verify:
- TestingAgent attempts = 2
- fallback output is used
- retry audit events exist
- risk is recorded

### 11. Rollback
`POST /api/orchestration/runs`

```json
{
  "scenario": "greenfield",
  "requirement": "Add expiration dates. [fail-implementation]"
}
```

Expected: `RolledBack`.

### 12. Safe-stop policy
`POST /api/orchestration/runs`

```json
{
  "scenario": "greenfield",
  "requirement": "Deploy directly to production."
}
```

Expected: `SafeStopped`.

### 13. Dynamic replanning
Start a normal run and copy its ID.

`POST /api/orchestration/runs/{id}/replan`

```json
{
  "updatedRequirement": "Add expiration dates and a configurable grace period.",
  "reason": "Requirement changed after architecture review."
}
```

Verify:
- new run ID
- higher `planVersion`
- `parentRunId` in context
- old requirement in context
- replan audit/decision lineage

### 14. Metrics
`GET /api/orchestration/metrics`

Verify counters for runs, retries, rollbacks and replans.
