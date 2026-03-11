using DemoFresh.Models;
using GitHub.Copilot.SDK;

namespace DemoFresh.Services;

public interface ICopilotSessionManager : IAsyncDisposable
{
    Task InitializeAsync();

    Task<CopilotSession> CreateAnalysisSessionAsync(string? workingDirectory = null);

    Task<CopilotSession> CreatePlanningSessionAsync(string? workingDirectory = null);

    Task<CopilotSession> CreateExecutionSessionAsync(string? workingDirectory = null);

    Task<string> SendAndWaitAsync(CopilotSession session, string prompt, List<UserMessageDataAttachmentsItem>? attachments = null);

    Task<ActionResult> DelegateToAgentAsync(Demo demo, string plan, CancellationToken ct = default);

    Task DisposeSessionAsync(CopilotSession session);
}
