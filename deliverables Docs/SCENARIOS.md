# Required Scenarios

## 1. Greenfield

Requirement:
`Add expiration dates to short URLs.`

What it demonstrates:
- requirement normalization
- architecture
- decomposition
- parallel implementation/test planning
- synchronized validation
- documentation
- release readiness
- human approval

## 2. Brownfield

Requirement:
`Update existing analytics to include the last time a short URL was accessed.`

What it demonstrates:
- impact mapping of existing model/repository/service/controller/tests
- smallest compatible change
- regression awareness
- same governed orchestration lifecycle

## 3. Ambiguous

Requirement:
`Make the URL shortener more secure.`

What it demonstrates:
- ambiguity detection
- explicit assumptions
- risk capture
- no silent invention of intent
- human review before completion

## Failure scenario

Requirement:
`Add expiration. [fail-testing]`

TestingAgent fails twice, then a bounded fallback creates a mandatory manual validation
plan. The risk is recorded and the workflow continues under stricter human review.

Requirement:
`Add expiration. [fail-implementation]`

ImplementationAgent fails twice. There is no safe implementation fallback, so completed
artifacts are rolled back and the run becomes `RolledBack`.

## Dynamic replan

Start a run, then call:
`POST /api/orchestration/runs/{id}/replan`

with an updated requirement. The old run is superseded/safe-stopped and a new run is
created with parent lineage and incremented plan version.
