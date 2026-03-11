using GitHub.Copilot.SDK;

namespace DemoFresh.Services;

public interface ICopilotSessionManager : IAsyncDisposable
{
    Task InitializeAsync();

    Task<CopilotSession> CreateAnalysisSessionAsync();

    Task<CopilotSession> CreatePlanningSessionAsync();

    Task<CopilotSession> CreateExecutionSessionAsync();

    Task<string> SendAndWaitAsync(CopilotSession session, string prompt, List<UserMessageDataAttachmentsItem>? attachments = null);

    Task DisposeSessionAsync(CopilotSession session);
}
