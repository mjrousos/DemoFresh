using System.ComponentModel;
using DemoFresh.Configuration;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DemoFresh.Services;

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
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = systemMessage
            },
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

            // For more granular control, you can implement custom permission handling logic here
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

    public Task<CopilotSession> CreateAnalysisSessionAsync() =>
        CreateSessionAsync(Prompts.Analysis);

    public Task<CopilotSession> CreatePlanningSessionAsync() =>
        CreateSessionAsync(Prompts.Planning);

    public Task<CopilotSession> CreateExecutionSessionAsync() =>
        CreateSessionAsync(Prompts.Execution);

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
