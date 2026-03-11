using System.ComponentModel;
using DemoFresh.Configuration;
using DemoFresh.Models;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DemoFresh.Services;

/// <summary>
/// Manages the full lifecycle of the Copilot SDK: client initialization, session creation
/// with configuration (system messages, tools, hooks, MCP servers), multi-turn messaging,
/// and cleanup. This is the central integration point for all Copilot SDK functionality.
/// </summary>
public sealed class CopilotSessionManager : ICopilotSessionManager
{
    private readonly ILogger<CopilotSessionManager> _logger;

    private readonly DemoFreshOptions _demoFreshConfig;

    private readonly ISmtpSender _smtpSender;
    
    private CopilotClient? _client;

    private const int CopilotTimeoutSeconds = 600;
    
    public CopilotSessionManager(ILogger<CopilotSessionManager> logger, IOptions<DemoFreshOptions> config, ISmtpSender smtpSender)
    {
        _logger = logger;
        _demoFreshConfig = config.Value;
        _smtpSender = smtpSender;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Copilot client");

        // The CopilotClient is designed to be long-lived and reused across multiple sessions, so we initialize it once here
        _client = new CopilotClient(new CopilotClientOptions());
        await _client.StartAsync();
        _logger.LogInformation("Copilot client started");
    }

    private async Task<CopilotSession> CreateSessionAsync(string systemMessage)
    {
        if (_client is null)
        {
            throw new InvalidOperationException("CopilotClient has not been initialized. Call InitializeAsync first.");
        }

        var config = new SessionConfig
        {
            Model = _demoFreshConfig.Model,

            // Append mode adds our custom system message to the SDK's built-in system message,
            // so the agent retains its default capabilities (tool use, coding) while gaining
            // domain-specific instructions for this session type
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = systemMessage
            },

            // Infinite sessions allow the SDK to automatically compact conversation history
            // when the context window fills up, preventing failures on long-running sessions.
            // BackgroundCompactionThreshold (80%) triggers proactive summarization of older messages.
            // BufferExhaustionThreshold (95%) triggers emergency compaction to prevent context overflow.
            InfiniteSessions = new InfiniteSessionConfig 
            { 
                Enabled = true,
                BackgroundCompactionThreshold = 0.8,
                BufferExhaustionThreshold = 0.95
            },
            Hooks = CreateHooks(),
            McpServers = CreateMcpServers(),
            Tools = CreateTools(),

            // The client name is an arbitrary string that will be included in User-Agent headers 
            // and can identify the source of sessions in telemetry
            ClientName = "DemoFresh",

            // PermissionHandler.ApproveAll auto-approves all agent actions (file edits, shell commands).
            // In production, implement custom logic to review and approve/deny specific operations
            // before the agent executes them.
            OnPermissionRequest = PermissionHandler.ApproveAll,

            // Other configuration options can be set here, as needed, such as:
            // AvailableTools = [],
            // CustomAgents = [],
            // SkillDirectories = [],
            // DisabledSkills = [],
            // ExcludedTools = [],            
            // OnUserInputRequest = DelegateForHandlingUserInputRequestsFromModel,
        
        };

        var session = await _client.CreateSessionAsync(config);

        _logger.LogInformation("Copilot session created: {SessionId}", session.SessionId);
        return session;
    }

    // Three session types, each with a different system message that constrains the agent's behavior:
    // - Analysis: identify demos and analyze drift from best practices (read-only)
    // - Planning: generate a remediation plan without executing changes (plan-only)
    // - Execution: execute a plan by making actual code changes
    public Task<CopilotSession> CreateAnalysisSessionAsync() =>
        CreateSessionAsync(Prompts.Analysis);

    public Task<CopilotSession> CreatePlanningSessionAsync() =>
        CreateSessionAsync(Prompts.Planning);

    public Task<CopilotSession> CreateExecutionSessionAsync() =>
        CreateSessionAsync(Prompts.Execution);

    // MCP (Model Context Protocol) servers extend the agent's capabilities with external tools.
    // The "stdio" transport means the SDK launches the MCP server as a child process and
    // communicates over stdin/stdout. Each server's tools become available to the agent.
    private Dictionary<string, object>? CreateMcpServers()
    {
        var mcpServers = new Dictionary<string, object>();

        if (_demoFreshConfig.Context7 is { Enabled: true, ApiKey.Length: > 0 })
        {
            var args = _demoFreshConfig.Context7.Args.Concat(["--api-key", _demoFreshConfig.Context7.ApiKey]).ToList();
            _logger.LogInformation("Context7 MCP server configured for session");
            mcpServers.Add("context7", new McpLocalServerConfig
            {
                Type = "stdio",
                Command = _demoFreshConfig.Context7.Command,
                Args = args,
                Tools = ["*"]
            });
        }
        else if (_demoFreshConfig.Context7 is null or { Enabled: false })
        {
            _logger.LogDebug("Context7 MCP is disabled; skipping");
        }
        else
        {
            _logger.LogWarning("Context7 MCP is enabled but no API key is configured; skipping. Set DemoFresh:Context7:ApiKey to enable.");
        }

        // Add additional MCP servers here as needed

        return mcpServers;
    }

    // AIFunctionFactory.Create turns a regular .NET delegate into an AIFunction that the Copilot
    // agent can invoke as a tool. The [Description] attributes on parameters tell the LLM what
    // each parameter is for. The second argument is the tool name, and the third is a description
    // the LLM uses to decide when to call this tool.
    private List<AIFunction> CreateTools()
    {
        var emailConfig = _demoFreshConfig.Email;
        var tools = new List<AIFunction>
        {
            AIFunctionFactory.Create(
                async ([Description("Recipient email address")] string recipient,
                       [Description("Email subject line")] string subject,
                       [Description("HTML email body")] string htmlBody) =>
                {
                    try
                    {
                        var message = new MimeMessage();
                        message.From.Add(new MailboxAddress(emailConfig.SenderName, emailConfig.SenderAddress));
                        message.To.Add(MailboxAddress.Parse(recipient));
                        message.Subject = subject;
                        message.Body = new TextPart("html") { Text = htmlBody };

                        await _smtpSender.SendAsync(message, emailConfig.SmtpHost, emailConfig.SmtpPort, emailConfig.UseSsl, emailConfig.SenderAddress, emailConfig.ClientId, emailConfig.ClientSecret);

                        _logger.LogInformation("Email sent to {Recipient} with subject \"{Subject}\"", recipient, subject);
                        return new { Success = true, Message = $"Email sent successfully to {recipient}" };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send email to {Recipient}", recipient);
                        return new { Success = false, Message = $"Failed to send email: {ex.Message}" };
                    }
                },
                "send_email",
                "Send an email report to the specified recipient")
        };

        // Add additional tools here as needed

        return tools;
    }

    // Hooks provide observability and control over session lifecycle events.
    // OnPreToolUse/OnPostToolUse intercept tool invocations — PreToolUseHookOutput can modify
    // or block tool calls if needed. Session start/end and error hooks enable logging and
    // custom error recovery strategies.
    private SessionHooks CreateHooks()
    {
        return new SessionHooks
        {
            OnPreToolUse = (input, invocation) =>
            {
                _logger.LogInformation(
                    "Tool invocation starting: {ToolName} with args {ToolArgs}",
                    input.ToolName,
                    input.ToolArgs);
                return Task.FromResult<PreToolUseHookOutput?>(new PreToolUseHookOutput());
            },
            OnPostToolUse = (input, invocation) =>
            {
                _logger.LogInformation(
                    "Tool invocation completed: {ToolName}",
                    input.ToolName);
                return Task.FromResult<PostToolUseHookOutput?>(new PostToolUseHookOutput());
            },
            OnSessionStart = (input, invocation) =>
            {
                _logger.LogInformation(
                    "Session started (source: {Source})",
                    input.Source);
                return Task.FromResult<SessionStartHookOutput?>(new SessionStartHookOutput());
            },
            OnSessionEnd = (input, invocation) =>
            {
                _logger.LogInformation(
                    "Session ended (reason: {Reason})",
                    input.Reason);
                return Task.FromResult<SessionEndHookOutput?>(new SessionEndHookOutput());
            },
            OnErrorOccurred = (input, invocation) =>
            {
                _logger.LogError(
                    "Session error: {Error} (context: {ErrorContext}, recoverable: {Recoverable})",
                    input.Error,
                    input.ErrorContext,
                    input.Recoverable);
                return Task.FromResult<ErrorOccurredHookOutput?>(new ErrorOccurredHookOutput
                {
                    ErrorHandling = "retry"
                });
            }
        };
    }

    // SendAndWaitAsync is a convenience pattern: send a user message and block until the agent
    // reaches an idle state (all tool calls complete, final response ready). This simplifies
    // multi-turn conversations compared to streaming individual events.
    public async Task<string> SendAndWaitAsync(
        CopilotSession session,
        string prompt,
        List<UserMessageDataAttachmentsItem>? attachments = null)
    {
        var options = new MessageOptions { Prompt = prompt };
        if (attachments is { Count: > 0 })
        {
            options.Attachments = attachments;
        }

        var response = await session.SendAndWaitAsync(options, TimeSpan.FromSeconds(CopilotTimeoutSeconds));
        return response?.Data?.Content ?? string.Empty;
    }

    public async Task<ActionResult> DelegateToAgentAsync(Demo demo, string plan, CancellationToken ct = default)
    {
        CopilotSession? session = null;

        try
        {
            _logger.LogInformation("Delegating execution for demo '{DemoName}' to coding agent", demo.Name);

            session = await CreateExecutionSessionAsync();

            // The "& " prefix is a Copilot CLI convention that tells the local agent to delegate
            // this task to the cloud-hosted coding agent for autonomous execution
            var prompt = $"& Please execute the following plan for demo '{demo.Name}':\n\n{plan}";
            var response = await SendAndWaitAsync(session, prompt);

            _logger.LogInformation("Delegation complete for demo '{DemoName}': {Response}", demo.Name, response);
            return new ActionResult(ActionResultType.Delegated, PrUrl: null, DelegationConfirmation: response, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delegate execution for demo '{DemoName}'", demo.Name);
            return new ActionResult(ActionResultType.Failed, PrUrl: null, DelegationConfirmation: null, ErrorMessage: ex.Message);
        }
        finally
        {
            if (session is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            _logger.LogInformation("Disposing Copilot client");
            await _client.DisposeAsync();
            _client = null;
        }
    }

    public async Task DisposeSessionAsync(CopilotSession session)
    {
        await session.DisposeAsync();
    }    
}
