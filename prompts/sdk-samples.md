# DemoFresh — Key Copilot SDK Usage Locations

This document catalogs the most important locations in the DemoFresh solution where the GitHub Copilot SDK is used. These are ideal code snippets to show during a demo of the SDK.

---

## 1. Client Initialization

**File:** `src/DemoFresh/Services/CopilotSessionManager.cs` (lines 35–40)

```csharp
public async Task InitializeAsync()
{
    _logger.LogInformation("Initializing Copilot client");
    _client = new CopilotClient(new CopilotClientOptions());
    await _client.StartAsync();
    _logger.LogInformation("Copilot client started");
}
```

**Why it matters:** This is the entry point for all SDK usage. `CopilotClient` manages the connection to the Copilot CLI server process. `StartAsync()` spawns the CLI and establishes a JSON-RPC channel. It's the "hello world" of the SDK — just two lines to get started.

---

## 2. Session Creation with System Message, Hooks, and Permissions

**File:** `src/DemoFresh/Services/CopilotSessionManager.cs` (lines 88–137)

```csharp
private async Task<CopilotSession> CreateSessionAsync(
    string model, string systemMessage,
    IEnumerable<AIFunction>? tools, Context7Config? context7 = null)
{
    var config = new SessionConfig
    {
        Model = model,
        SystemMessage = new SystemMessageConfig
        {
            Mode = SystemMessageMode.Append,
            Content = systemMessage
        },
        Hooks = CreateHooks(),
        OnPermissionRequest = PermissionHandler.ApproveAll
    };

    if (tools is not null)
        config.Tools = [.. tools];

    // ... MCP server setup (see #6) ...

    var session = await _client.CreateSessionAsync(config);
    return session;
}
```

**Why it matters:** This demonstrates the richest part of the SDK API — `SessionConfig`. Key concepts shown:
- **Model selection** — choose the LLM at runtime
- **System messages** — `Append` mode adds instructions without replacing the built-in guardrails
- **Permission handling** — `PermissionHandler.ApproveAll` grants the agent autonomy (vs. custom approval logic)
- **Hooks** — lifecycle callbacks for observability (see #3)
- **Custom tools** — registered via the `Tools` property (see #5)

---

## 3. Session Hooks for Observability

**File:** `src/DemoFresh/Services/CopilotSessionManager.cs` (lines 140–186)

```csharp
private SessionHooks CreateHooks()
{
    return new SessionHooks
    {
        OnPreToolUse = (input, invocation) =>
        {
            _logger.LogInformation("Tool invocation starting: {ToolName}", input.ToolName);
            return Task.FromResult<PreToolUseHookOutput?>(new PreToolUseHookOutput());
        },
        OnPostToolUse = (input, invocation) =>
        {
            _logger.LogInformation("Tool invocation completed: {ToolName}", input.ToolName);
            return Task.FromResult<PostToolUseHookOutput?>(new PostToolUseHookOutput());
        },
        OnErrorOccurred = (input, invocation) =>
        {
            _logger.LogError("Session error: {Error}", input.Error);
            return Task.FromResult<ErrorOccurredHookOutput?>(new ErrorOccurredHookOutput
            {
                ErrorHandling = "retry"
            });
        }
    };
}
```

**Why it matters:** Hooks let you intercept every tool call, session lifecycle event, and error — without modifying the agent's behavior. This is critical for production observability. The `OnPreToolUse` hook could also be used to deny or modify tool calls (e.g., blocking file writes in read-only mode).

---

## 4. Sending Prompts and Waiting for Responses

**File:** `src/DemoFresh/Services/CopilotSessionManager.cs` (lines 58–71)

```csharp
public async Task<string> SendAndWaitAsync(
    CopilotSession session, string prompt,
    List<UserMessageDataAttachmentsItem>? attachments = null)
{
    var options = new MessageOptions { Prompt = prompt };
    if (attachments is { Count: > 0 })
        options.Attachments = attachments;

    var response = await session.SendAndWaitAsync(options,
        TimeSpan.FromSeconds(CopilotTimeoutSeconds));
    return response?.Data?.Content ?? string.Empty;
}
```

**Why it matters:** This is the core send/receive pattern. `SendAndWaitAsync` sends a prompt and blocks until the agent finishes its entire reasoning + tool-use loop and returns an idle response. The `MessageOptions` also supports file attachments. This single method enables the multi-turn conversation pattern used throughout DemoFresh.

---

## 5. Custom Tools via AIFunctionFactory

**File:** `src/DemoFresh/Tools/EmailToolFactory.cs` (lines 12–41)

```csharp
public static AIFunction Create(EmailConfig config, ISmtpSender smtpSender, ILogger logger)
{
    return AIFunctionFactory.Create(
        async ([Description("Recipient email address")] string recipient,
               [Description("Email subject line")] string subject,
               [Description("HTML email body")] string htmlBody) =>
        {
            // ... build MimeMessage, send via SMTP ...
            return new { Success = true, Message = $"Email sent to {recipient}" };
        },
        "send_email",
        "Send an email report to the specified recipient");
}
```

**Why it matters:** This shows how to give the Copilot agent new capabilities. `AIFunctionFactory.Create` from `Microsoft.Extensions.AI` turns any C# lambda into a tool the LLM can call. The `[Description]` attributes become the tool's parameter documentation that the LLM reads to understand how to invoke it. When the agent decides to call `send_email`, this code runs in your process — the agent never sees your SMTP credentials.

---

## 6. MCP Server Configuration (Context7)

**File:** `src/DemoFresh/Services/CopilotSessionManager.cs` (lines 118–132)

```csharp
if (context7 is { Enabled: true, ApiKey.Length: > 0 })
{
    var args = context7.Args.Concat(["--api-key", context7.ApiKey]).ToList();
    config.McpServers = new Dictionary<string, object>
    {
        ["context7"] = new McpLocalServerConfig
        {
            Type = "stdio",
            Command = context7.Command,
            Args = args,
            Tools = ["*"]
        }
    };
}
```

**Why it matters:** MCP (Model Context Protocol) servers extend the agent's capabilities with external tools. Here, Context7 is configured as a local stdio server that provides up-to-date library documentation. The SDK launches the MCP server process automatically and makes its tools available to the agent. This is how you connect Copilot to any external system — databases, APIs, proprietary tools — via the MCP standard.

---

## 7. Multi-Turn Planning (Plan Then Execute)

**File:** `src/DemoFresh/Services/PlanExecutor.cs` (lines 9–39)

```csharp
// Turn 1: Generate a plan (planning session — "don't execute, only plan")
public async Task<string> GeneratePlanAsync(Demo demo, IReadOnlyList<DriftFinding> findings, string model, ...)
{
    var session = await sessionManager.CreatePlanningSessionAsync(model);
    await using var _ = session;
    var plan = await sessionManager.SendAndWaitAsync(session, BuildPlanningPrompt(demo, findings));
    return plan;
}

// Turn 2: Execute the plan (execution session — different system message)
public async Task ExecutePlanAsync(string plan, string workingDirectory, string model, ...)
{
    var session = await sessionManager.CreateExecutionSessionAsync(model);
    await using var _ = session;
    await sessionManager.SendAndWaitAsync(session, $"Working directory: {workingDirectory}\n\nExecute the following plan:\n\n{plan}");
}
```

**Why it matters:** This demonstrates the SDK's most powerful pattern — multi-turn orchestration with different session configurations. The planning session uses a system message that says "plan only, don't execute." The execution session uses a different system message that says "execute the plan." This gives you programmatic control over the agent's behavior that isn't possible with interactive chat.

---

## 8. Delegation to the Cloud Coding Agent

**File:** `src/DemoFresh/Services/DelegationService.cs` (lines 9–23)

```csharp
public async Task<ActionResult> DelegateToAgentAsync(Demo demo, string plan, string model, ...)
{
    session = await sessionManager.CreateExecutionSessionAsync(model);

    // The "&" prefix tells the CLI to delegate to the cloud coding agent
    var prompt = $"& Please execute the following plan for demo '{demo.Name}':\n\n{plan}";
    var response = await sessionManager.SendAndWaitAsync(session, prompt);

    return new ActionResult(ActionResultType.Delegated, DelegationConfirmation: response, ...);
}
```

**Why it matters:** The `&` prefix is a convention that tells the Copilot CLI to offload work to the cloud-based coding agent. This means DemoFresh can hand off complex, long-running tasks to run asynchronously on GitHub's infrastructure — the cloud agent creates branches, makes changes, and opens PRs on its own. The SDK makes this a one-line change from local execution.

---

## 9. Registering a Custom Tool with a Session

**File:** `src/DemoFresh/Services/AnalysisOrchestrator.cs` (lines 139–148)

```csharp
private async Task SendEmailReportAsync(AnalysisReport report, CancellationToken ct)
{
    var htmlReport = _reportGenerator.GenerateHtmlReport(report);
    var emailTool = EmailToolFactory.Create(_options.Email, _smtpSender, _logger);

    var session = await _sessionManager.CreateAnalysisSessionAsync(_options.Model, tools: [emailTool]);
    var prompt = $"Send an email to {_options.ReportRecipient} with subject ...";
    await _sessionManager.SendAndWaitAsync(session, prompt);
}
```

**Why it matters:** This shows the full end-to-end pattern: create a tool → register it with a session → prompt the agent to use it. The agent reads the tool's name and description, decides it matches the user's intent, and calls it with the right parameters. Your code handles the actual email sending. The agent never has direct access to SMTP — it just knows "there's a `send_email` tool I can call."

---

## 10. Multi-Turn Conversation (Demo Identification → Drift Analysis)

**File:** `src/DemoFresh/Services/DriftAnalyzer.cs` (lines 20–57, 60–96)

```csharp
// Turn 1: Identify demos in the repo
public async Task<IReadOnlyList<Demo>> IdentifyDemosAsync(RepoContents repo, string model, ...)
{
    var session = await sessionManager.CreateAnalysisSessionAsync(model, context7);
    var response = await sessionManager.SendAndWaitAsync(session, BuildIdentifyDemosPrompt(repo));
    var demos = JsonSerializer.Deserialize<List<Demo>>(ExtractJsonArray(response), JsonOptions);
    return demos;
}

// Turn 2 (per demo): Analyze for drift
public async Task<IReadOnlyList<DriftFinding>> AnalyzeDriftAsync(Demo demo, RepoContents repo, ...)
{
    var session = await sessionManager.CreateAnalysisSessionAsync(model, context7);
    var response = await sessionManager.SendAndWaitAsync(session, BuildAnalyzeDriftPrompt(demo, repo));
    var findings = JsonSerializer.Deserialize<List<DriftFinding>>(ExtractJsonArray(response), JsonOptions);
    return findings;
}
```

**Why it matters:** This shows structured data extraction from LLM responses. The prompts include JSON schemas and examples (see the full file for details), and the code parses the JSON response into strongly-typed C# records. The `ExtractJsonArray` helper handles cases where the LLM wraps JSON in markdown code fences. This pattern — prompt with schema → parse response → use typed objects — is the foundation of programmatic AI workflows.
