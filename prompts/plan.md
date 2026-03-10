# DemoFresh Implementation Plan

## Problem Statement

Build a .NET 10 command-line tool ("DemoFresh") that uses the GitHub Copilot SDK to automatically keep demo code and learning documentation up to date. It clones repos, identifies demos, finds drift from best practices, plans/executes updates via PRs or cloud coding agent delegation, and emails a report.

## Design Decisions

- **Single .NET project** for simplicity (demo purposes)
- **System message + multi-turn** approach for SDK "planning" (no native plan mode API)
- **LLM-driven demo identification** — the Copilot agent itself identifies demos from repo contents
- **One PR per demo** — grouped findings
- **GitHub CLI auth** (`gh auth token`) — no PAT management needed
- **SMTP** for email (Gmail SMTP with app password — no Azure AD app registration needed), registered as a Copilot SDK custom tool
- **Delegation to coding agent** via sending delegation prompts through the SDK session (CLI-mediated, not a direct API)
- **Freshness score excluded** — reserved for live demo feature addition

## Architecture

```
DemoFresh (Console App / .NET Generic Host)
├── Program.cs                    — Entry point, host setup, CLI arg parsing
├── appsettings.json              — Config (repos, email, action mode, model)
├── Configuration/
│   ├── DemoFreshOptions.cs       — Strongly-typed config classes
│   └── RepoConfig.cs             — Per-repo configuration model
├── Services/
│   ├── RepoService.cs            — Clone repos, enumerate files
│   ├── AnalysisOrchestrator.cs   — Main workflow orchestration
│   ├── CopilotSessionManager.cs  — Copilot SDK client/session lifecycle
│   ├── DriftAnalyzer.cs          — Send analysis prompts, parse drift results
│   ├── PlanExecutor.cs           — Multi-turn plan generation & execution
│   ├── PrService.cs              — Create PRs via gh CLI
│   ├── DelegationService.cs      — Delegate to coding agent via SDK session
│   └── ReportGenerator.cs        — Build report from analysis results
├── Tools/
│   ├── EmailTool.cs              — SMTP email tool via MailKit (registered with SDK)
│   └── WebSearchTool.cs          — Web search tool (if not built-in)
├── Models/
│   ├── Demo.cs                   — Identified demo model
│   ├── DriftFinding.cs           — Individual drift finding
│   ├── AnalysisReport.cs         — Full analysis report
│   └── ActionResult.cs           — PR/delegation result
└── Extensions/
    └── ServiceCollectionExtensions.cs — DI registration helpers
```

## Todos

### 1. `project-setup` — Initialize .NET project and dependencies
- `dotnet new console -n DemoFresh --framework net10.0`
- Add NuGet packages: `GitHub.Copilot.SDK`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.AI`, `MailKit`, `Serilog.Extensions.Hosting`, `Serilog.Sinks.File` (for file logging)
- Set up `Program.cs` with generic host builder
- Add `appsettings.json` with config schema
- Create `.gitignore` for .NET

### 2. `configuration` — Configuration models and CLI arg parsing
**Depends on:** `project-setup`
- Create `DemoFreshOptions` with properties: `Repos` (list), `EmailConfig`, `ActionMode` (CreatePR | DelegateToCodingAgent), `Model`, `ReportRecipient`
- Create `RepoConfig` with `Url`, `Branch`, `Name`
- Parse CLI args: `--repo <url>` for ad-hoc, or use configured repos from appsettings
- Bind config via `IOptions<DemoFreshOptions>` in DI

### 3. `repo-service` — Clone and enumerate repo files
**Depends on:** `configuration`
- Clone repo to temp directory using `gh repo clone` or `git clone`
- Enumerate relevant files (source code, markdown, config files)
- Return file listing with contents for analysis
- Clean up temp directories on completion

### 4. `copilot-session-manager` — Copilot SDK client/session lifecycle
**Depends on:** `project-setup`
- Create `CopilotSessionManager` wrapping `CopilotClient` and `CopilotSession`
- Configure `SessionConfig` with model selection, streaming, and system message for planning behavior
- System message instructs the agent to: identify demos, analyze for drift, produce structured JSON output
- Configure session hooks for structured logging:
  - `OnPreToolUse` — log tool name and args
  - `OnPostToolUse` — log tool completion
  - `OnSessionStart` / `OnSessionEnd` — log session lifecycle
  - `OnErrorOccurred` — log errors with context
  - All hooks use `ILogger` injected via DI
- Handle session lifecycle: start, create, dispose
- Register custom tools (email, potentially web search)
- Implement event handling for `AssistantMessageEvent`, `SessionIdleEvent`, `ToolExecutionCompleteEvent`, errors

### 5. `drift-analyzer` — Demo identification and drift analysis
**Depends on:** `repo-service`, `copilot-session-manager`
- **Turn 1 — Identify demos:** Send repo file listing to Copilot session, ask it to identify discrete demos/concepts. Parse structured response into `Demo` objects.
- **Turn 2 — Analyze drift:** For each identified demo, send a follow-up prompt asking Copilot to:
  - Check URLs found in the demo files for current validity
  - Search the web for current best practices for the identified concepts
  - Compare existing code against current best practices
  - Return structured `DriftFinding` objects (description, severity, affected files, suggested fix)
- Handle cases where no drift is found (skip to reporting)

### 6. `plan-executor` — Multi-turn planning and execution
**Depends on:** `drift-analyzer`
- **Planning turn:** Send drift findings to a planning session with system message: "Produce a structured implementation plan. Do not make changes yet."
- Parse the plan from the response
- **Execution turn (CreatePR mode):** Send "Execute the plan" prompt in a session configured with write tools, pointed at a new branch
- Collect changed files for PR creation

### 7. `pr-service` — Create PRs via GitHub CLI
**Depends on:** `plan-executor`
- Create a new branch per demo: `demofresh/<demo-name>-<date>`
- Commit changes with descriptive message
- Push branch and create PR using `gh pr create`
- Return PR URL for reporting

### 8. `delegation-service` — Delegate to coding agent
**Depends on:** `plan-executor`
- Alternative to `pr-service` based on `ActionMode` config
- Send delegation prompt through SDK session with plan details
- The underlying CLI handles offloading to the cloud coding agent
- Capture delegation confirmation/link for reporting

### 9. `email-tool` — SMTP email tool (Copilot SDK custom tool)
**Depends on:** `copilot-session-manager`
- Register as an `AIFunction` via `AIFunctionFactory.Create`
- Parameters: `recipient`, `subject`, `htmlBody`
- Use `System.Net.Mail.SmtpClient` (or `MailKit` for modern TLS support) with Gmail SMTP (`smtp.gmail.com:587`)
- SMTP credentials (Gmail address + app password) stored in appsettings.json (or user secrets for dev)
- Return confirmation to the Copilot session

### 10. `report-generator` — Build and send analysis report
**Depends on:** `email-tool`, `drift-analyzer`
- Aggregate results: demos found, drift findings per demo, actions taken (PR links or delegation confirmations)
- Generate HTML report body
- Trigger email via the Copilot session (the agent calls the email tool)
- Also output a console summary

### 11. `analysis-orchestrator` — Main workflow orchestration
**Depends on:** all services above
- Wire up the end-to-end flow as a `BackgroundService` or hosted service:
  1. Load config (CLI args + appsettings)
  2. For each repo: clone → analyze → plan → execute/delegate → report
  3. Handle errors per-repo (continue processing remaining repos)
  4. Exit with appropriate status code

### 12. `tests` — Unit and integration tests
**Depends on:** all implementation todos
- Create `DemoFresh.Tests` xUnit project
- **Unit tests:**
  - Config parsing (CLI args, appsettings binding)
  - Report generation (given findings, produces correct HTML)
  - RepoService file enumeration logic
  - DriftFinding model serialization
- **Integration tests:**
  - CopilotSessionManager lifecycle (start, send, dispose) — requires Copilot CLI installed
  - End-to-end orchestrator with a small test repo
  - Email tool registration and invocation (mocked SMTP client)
- Mock `CopilotClient` where possible for unit-testable session interactions

### 13. `copilot-instructions` — Repo instructions and skills for CLI demo
**Depends on:** `project-setup`
- Create `.github/copilot-instructions.md` with project-specific guidance:
  - Project overview and architecture
  - Coding conventions (async/await patterns, DI usage, error handling)
  - How to use the Copilot SDK correctly in this project
- Create instruction files in `.github/instructions/` for specific patterns
- These will be shown during the live demo portion

## Dependency Graph

```
project-setup
├── configuration → repo-service ──┐
├── copilot-session-manager ───────┤
│   └── email-tool ────────────────┤
│                                  ├── drift-analyzer → plan-executor ─┬── pr-service ──────┐
│                                  │                                   └── delegation-service┤
│                                  └── report-generator ───────────────────────────────────────┤
│                                                                                              ├── analysis-orchestrator → tests
└── copilot-instructions (parallel)                                                            │
```

## Notes

- The SDK is in technical preview — API surface may shift. Pin the NuGet package version.
- Delegation to the coding agent is CLI-mediated, not a direct SDK method. We send prompts like `& <task description>` through the session.
- For the live demo: the "freshness score" feature is intentionally omitted so it can be added live using Copilot CLI plan mode + autopilot.
- Session hooks (`OnPreToolUse`, `OnPostToolUse`, `OnSessionStart`, `OnSessionEnd`, `OnErrorOccurred`) used for structured logging via `Microsoft.Extensions.Logging` to file.
- Consider using `InfiniteSessions` for repos with very large file sets to avoid context exhaustion.
