using DemoFresh.Configuration;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DemoFresh.Services;

public sealed class CopilotSessionManager : ICopilotSessionManager
{
    private readonly ILogger<CopilotSessionManager> _logger;
    private CopilotClient? _client;

    private const string AnalysisSystemMessage =
        "You are an expert at analyzing code repositories. Identify demos and concepts being taught. " +
        "When analyzing for drift, check URLs for validity, search the web for current best practices, " +
        "and compare existing code. Return results as structured JSON. " +
        "Always use Context7 MCP when you need library/API documentation, code generation, setup or configuration steps without being explicitly asked.";

    private const string PlanningSystemMessage =
        "You are a planning assistant. Given drift findings, produce a structured implementation plan. " +
        "Do NOT make any changes yet. Only plan.";

    private const string ExecutionSystemMessage =
        "Execute the provided plan. Make the necessary code changes.";

    public CopilotSessionManager(ILogger<CopilotSessionManager> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Copilot client");
        _client = new CopilotClient(new CopilotClientOptions());
        await _client.StartAsync();
        _logger.LogInformation("Copilot client started");
    }

    public Task<CopilotSession> CreateAnalysisSessionAsync(string model, Context7Config? context7 = null, IEnumerable<AIFunction>? tools = null)
    {
        return CreateSessionAsync(model, AnalysisSystemMessage, tools, context7);
    }

    public Task<CopilotSession> CreatePlanningSessionAsync(string model)
    {
        return CreateSessionAsync(model, PlanningSystemMessage, tools: null);
    }

    public Task<CopilotSession> CreateExecutionSessionAsync(string model, IEnumerable<AIFunction>? tools = null)
    {
        return CreateSessionAsync(model, ExecutionSystemMessage, tools);
    }

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

        var response = await session.SendAndWaitAsync(options);
        return response?.Data?.Content ?? string.Empty;
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

    private async Task<CopilotSession> CreateSessionAsync(
        string model,
        string systemMessage,
        IEnumerable<AIFunction>? tools,
        Context7Config? context7 = null)
    {
        if (_client is null)
        {
            throw new InvalidOperationException("CopilotClient has not been initialized. Call InitializeAsync first.");
        }

        var config = new SessionConfig
        {
            Model = model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = systemMessage
            },
            Hooks = CreateHooks(),

            // For more granular control, you can implement custom permission handling logic here
            OnPermissionRequest = PermissionHandler.ApproveAll
        };

        if (tools is not null)
        {
            config.Tools = [.. tools];
        }

        if (context7 is { Enabled: true, ApiKey.Length: > 0 })
        {
            var args = context7.Args.Concat(["--api-key", context7.ApiKey]).ToList();
            config.McpServers = new Dictionary<string, object>
            {
                ["context7"] = new McpLocalServerConfig
                {
                    Type = "stdio",
                    Command = context7.Command,
                    Args = args
                }
            };
            _logger.LogInformation("Context7 MCP server configured for session");
        }

        _logger.LogInformation("Creating Copilot session with model {Model}", model);
        var session = await _client.CreateSessionAsync(config);
        _logger.LogInformation("Copilot session created: {SessionId}", session.SessionId);
        return session;
    }

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
}
