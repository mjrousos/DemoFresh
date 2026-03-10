# DemoFresh - Copilot Instructions

## Project Overview
DemoFresh is a .NET 10 command-line tool that uses the GitHub Copilot SDK to automatically keep demo code and learning documentation up to date with current best practices. It clones repos, identifies demos, analyzes drift from best practices, and creates PRs or delegates to the coding agent.

## Build, Test, and Run

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run a single test by name
dotnet test --filter "FullyQualifiedName~DemoFresh.Tests.ConfigurationTests.DemoFreshOptions_DefaultValues_AreCorrect"

# Run tests in a single class
dotnet test --filter "FullyQualifiedName~DemoFresh.Tests.DriftAnalyzerTests"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run the app
dotnet run --project src/DemoFresh -- --repo https://github.com/owner/repo

# Test email sending in isolation
dotnet run --project src/DemoFresh -- --test-email
```

## Architecture
- Single .NET 10 console app using the .NET Generic Host pattern
- Copilot SDK for AI-powered analysis and planning
- MailKit for SMTP email (Gmail OAuth2)
- Serilog for structured file logging

### Service Responsibilities & Workflow
The main workflow is orchestrated by `AnalysisOrchestrator` (a `BackgroundService`):

1. `RepoService` — Clones repos via `IProcessRunner` (git), enumerates source files, filters by extension/size/directory
2. `DriftAnalyzer` — **Turn 1:** Sends file listing to Copilot to identify demos (returns `Demo` objects). **Turn 2:** Per-demo drift analysis (returns `DriftFinding` objects). Uses Context7 MCP (when enabled) for up-to-date library documentation and web search for broader best practice analysis. Expects structured JSON responses from the LLM.
3. `PlanExecutor` — **Planning turn:** Sends findings to a planning session (system message: "plan only, don't execute"). **Execution turn:** Sends plan to an execution session pointed at the working directory.
4. `PrService` — Creates git branches (`demofresh/<name>-<date>`), commits, pushes, and opens PRs via `IProcessRunner` (git/gh)
5. `DelegationService` — Sends `& <plan>` prompt through an SDK session (the `&` prefix tells the CLI to delegate to the cloud coding agent)
6. `ReportGenerator` — Builds HTML email reports (with severity color coding) and console summaries
7. `EmailToolFactory` — Creates an `AIFunction` (`send_email`) that the Copilot agent can invoke to send emails via `ISmtpSender`

### Thin Wrapper Pattern (Testability)
External dependencies are wrapped in thin interfaces so all business logic can be unit tested:
- `IProcessRunner` / `ProcessRunner` — Wraps `System.Diagnostics.Process` for git/gh CLI commands. Used by `RepoService` and `PrService`.
- `ISmtpSender` / `SmtpSender` — Wraps MailKit `SmtpClient` with Google OAuth2 authentication. Used by `EmailToolFactory`.
- `ICopilotSessionManager` / `CopilotSessionManager` — Wraps `CopilotClient`/`CopilotSession`. Used by `DriftAnalyzer`, `PlanExecutor`, `DelegationService`.

When adding new external dependencies, always follow this pattern: create an interface, implement a thin wrapper, inject via DI, and mock in tests.

### Context7 MCP Integration
- Context7 provides up-to-date, version-specific library documentation via MCP
- Configured per-session via `SessionConfig.McpServers` when `Context7Config.Enabled` is true
- Launches locally via `npx -y @upstash/context7-mcp --api-key <key>` (stdio transport)
- API key is read from `DemoFreshOptions.Context7.ApiKey` (use .NET user secrets in development)
- Analysis system messages instruct the agent to always use Context7 for library/API documentation
- If Context7 is disabled or unavailable, the agent falls back to web search

### Graceful Degradation
Optional features are skipped with a warning log when not configured:
- **Context7**: Skipped if `ApiKey` is empty or `Enabled` is false — drift analysis proceeds with web search only
- **Email**: Skipped if `SenderAddress`, `ClientId`, `ClientSecret`, or `ReportRecipient` is missing — console summary still prints

### Data Models
All models are immutable records in `Models/`:
- `RepoContents` (WorkingDirectory, RepoUrl, Branch, Files) → `RepoFile` (RelativePath, Content, Extension)
- `Demo` (Name, Description, Concepts, FilePaths)
- `DriftFinding` (Description, Severity, AffectedFiles, SuggestedFix, Category) with `DriftSeverity` enum (Low/Medium/High/Critical)
- `AnalysisReport` (RepoUrl, Branch, AnalyzedAt, Demos) → `DemoAnalysis` (Demo, Findings, Plan, Action)
- `ActionResult` (Type, PrUrl, DelegationConfirmation, ErrorMessage) with `ActionResultType` enum (PrCreated/Delegated/NoActionNeeded/Failed)

### Configuration
- `DemoFreshOptions` binds from the `DemoFresh` section of appsettings.json
- `--repo <url>` CLI argument overrides the configured repo list via `PostConfigure<DemoFreshOptions>`
- `--test-email` CLI argument sends a test email directly and exits (for debugging OAuth2)
- `ActionMode` enum: `CreatePR` (local execution + PR) or `DelegateToCodingAgent` (cloud delegation)
- Email and Context7 credentials should use .NET user secrets in development

## Coding Conventions
- Use async/await throughout — all I/O operations must be async
- Use dependency injection (constructor injection via primary constructors) for all services
- Use `IOptions<T>` for configuration access
- Use `ILogger<T>` (via Microsoft.Extensions.Logging) for all logging with structured message templates (not string interpolation)
- Use records for immutable data models
- Use nullable reference types — all nullable types must be explicitly marked with `?`
- Prefer pattern matching and switch expressions
- Keep methods focused — single responsibility
- Error handling: catch specific exceptions, log with context, and continue processing where possible (per-repo error isolation in AnalysisOrchestrator)

## Copilot SDK Patterns
- Custom tools are registered via `AIFunctionFactory.Create` from Microsoft.Extensions.AI
- Tool parameters use `[Description("...")]` attributes for LLM understanding
- System messages use `SystemMessageConfig` with `Mode = SystemMessageMode.Append`
- Three session types with tailored system messages: analysis, planning, execution (see `CopilotSessionManager`)
- Session hooks log tool invocations, lifecycle events, and errors via `ILogger`
- Multi-turn conversations: `SendAndWaitAsync` sends a prompt and waits for `SessionIdleEvent`
- Planning uses a two-turn approach: first turn generates plan, second turn executes
- Delegation to the coding agent uses the `&` prompt prefix convention
- JSON extraction from LLM responses: find first `[` to last `]` to handle markdown code blocks

## Testing Patterns
- Use xUnit with `[Fact]` and `[Theory]` attributes
- Mock interfaces with `Moq` (Mock<IProcessRunner>, Mock<ICopilotSessionManager>, Mock<ISmtpSender>)
- Use standard xUnit `Assert.*` methods for assertions (do NOT use FluentAssertions due to licensing concerns)
- Use `NullLogger<T>` from Microsoft.Extensions.Logging.Abstractions for test loggers
- Use `TestDataHelpers` for shared test data factory methods (CreateTestDemo, CreateTestFinding, CreateTestReport)
- Integration tests that require Copilot CLI should be marked with `[Trait("Category", "Integration")]`
- Thin wrappers (ProcessRunner, SmtpSender, CopilotSessionManager) are intentionally not unit tested — they are covered by integration tests
