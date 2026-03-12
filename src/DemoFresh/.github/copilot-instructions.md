# DemoFresh – Copilot Instructions

## Project Overview
DemoFresh is a **.NET 10 console application** that uses the **GitHub Copilot SDK** to automatically keep demo code and learning documentation current with best practices. It clones repos, identifies demos, analyzes drift, and either creates PRs or delegates to the cloud coding agent.

## Tech Stack
- **Runtime:** .NET 10, C# with nullable references and implicit usings
- **AI:** GitHub.Copilot.SDK (0.1.32), Microsoft.Extensions.AI (10.4.0), Claude Sonnet 4.6
- **Email:** MailKit + Google.Apis.Auth (Gmail OAuth2)
- **Logging:** Serilog (console + daily-rolling file sink)
- **Hosting:** Microsoft.Extensions.Hosting (Generic Host / BackgroundService)
- **Testing:** xUnit 2.9.3, Moq 4.20.72, coverlet

## Solution Layout
```
DemoFresh.slnx
├── src/DemoFresh/          # Main console application
│   ├── Configuration/      # Strongly-typed options (DemoFreshOptions, EmailConfig, Context7Config, …)
│   ├── Extensions/         # ServiceCollectionExtensions.cs (DI wiring)
│   ├── Models/             # Immutable records (Demo, DriftFinding, AnalysisReport, …)
│   ├── Services/           # All business logic + thin-wrapper interfaces
│   ├── Program.cs          # Host builder, CLI args, Serilog setup
│   ├── Prompts.cs          # Centralized system-message strings
│   └── appsettings.json    # Config (repos, model, email, Context7)
└── tests/DemoFresh.Tests/  # xUnit unit tests + IntegrationTests/ subdirectory
```

## Build / Test / Run
```bash
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~DemoFresh.Tests.DriftAnalyzerTests"
dotnet test --collect:"XPlat Code Coverage"
dotnet run --project src/DemoFresh -- --repo https://github.com/owner/repo
dotnet run --project src/DemoFresh -- --test-email
```

## Architecture & Workflow
`AnalysisOrchestrator` (BackgroundService) drives the pipeline:
1. **RepoService** – shallow-clones repo via `IProcessRunner` (git), enumerates source files
2. **DriftAnalyzer** – two Copilot turns: identify `Demo` objects, then produce `DriftFinding` objects (uses Context7 MCP for live library docs)
3. **PlanExecutor** – planning turn (system msg: "plan only") then execution turn in working directory
4. **PrService** – branch `demofresh/<name>-<date>` → commit → push → `gh pr create`
5. **DelegationService** – sends `& <plan>` to cloud coding agent
6. **ReportGenerator** – styled HTML email + console summary
7. **EmailToolFactory** – exposes `send_email` as an `AIFunction` the agent can invoke

## Thin-Wrapper Pattern (Testability)
All external I/O is behind interfaces; concrete wrappers are intentionally untested (covered by integration tests only):
- `IProcessRunner` / `ProcessRunner` – wraps `System.Diagnostics.Process`
- `ISmtpSender` / `SmtpSender` – wraps MailKit with Google OAuth2
- `ICopilotSessionManager` / `CopilotSessionManager` – wraps CopilotClient/CopilotSession

## Coding Conventions
- **Async/await** for all I/O; return `Task` or `Task<T>`
- **Constructor injection** via primary constructors; `IOptions<T>` for config, `ILogger<T>` for logging
- **Structured logging** with message templates — never string interpolation in log calls
- **Immutable records** for all data models in `Models/`
- **Nullable reference types** — all nullable types explicitly marked `?`
- Prefer **pattern matching** and **switch expressions**
- New external dependencies must follow the thin-wrapper pattern with a matching interface

## Copilot SDK Patterns
- Tools registered via `AIFunctionFactory.Create`; parameters use `[Description("...")]` attributes
- Three session types: analysis (read-only), planning (no execution), execution (code changes)
- `SystemMessageConfig` with `Mode = SystemMessageMode.Append`
- Multi-turn: `SendAndWaitAsync` → wait for `SessionIdleEvent`
- JSON extraction from responses: find first `[` to last `]` (handles markdown fences)
- Context7 MCP configured per-session via `SessionConfig.McpServers` when `Context7Config.Enabled`

## Testing Conventions
- **xUnit** `[Fact]` / `[Theory]`; **Moq** for mocks; **NullLogger\<T\>** for loggers
- Do **not** use FluentAssertions (licensing); use standard `Assert.*` methods
- Shared test data: `TestDataHelpers.CreateTestDemo()`, `CreateTestFinding()`, `CreateTestReport()`
- Integration tests: `[Trait("Category", "Integration")]` in `tests/DemoFresh.Tests/IntegrationTests/`
- Test naming: `MethodName_Scenario_ExpectedOutcome`

## Configuration Notes
- Credentials (email OAuth2, Context7 API key) via **.NET User Secrets** in development (secret ID: `29073e3a-4e83-4c80-9866-7bba29e551ea`)
- `--repo <url>` CLI arg overrides configured repos via `PostConfigure<DemoFreshOptions>`
- `ActionMode`: `CreatePR` (local execution + PR) or `DelegateToCodingAgent`
- Optional features (email, Context7) degrade gracefully — skipped with a warning log when unconfigured