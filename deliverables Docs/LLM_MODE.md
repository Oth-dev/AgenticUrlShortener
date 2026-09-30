# Local LLM mode — interview walkthrough

The default deterministic mode is for automated tests. Enable Llm.Enabled
in appsettings.json after installing Ollama and pulling llama3.2:3b.

RequirementAgent and ArchitectureAgent call a real local LLM through HTTP
POST /api/generate. Responses are generated text (temperature 0.7), not
predefined C# strings. The baseline deterministic analysis is preserved.

AgenticOrchestrator still owns dependency graph scheduling, Task.WhenAll,
retries, fallback/rollback, audit, policy gates and human approval.
The model cannot call tools or approve/release a run.

Demo: run greenfield, brownfield and ambiguous scenarios twice and compare
LLM ANALYSIS in step outputs. The wording may vary. Verify that all runs
still stop at WaitingForApproval until a human approves.

No external paid API is needed, but Ollama needs local hardware.
If Ollama is not running or the model is missing, a call fails and the
orchestrator handles it as an agent failure.

For production: typed JSON output schemas; validation and sanitization;
restricted tool permissions; prompt-injection defense; bounded context;
authentication; persistent stores; observability and budget controls.
