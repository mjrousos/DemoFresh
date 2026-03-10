using DemoFresh.Configuration;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;

namespace DemoFresh.Services;

public interface ICopilotSessionManager : IAsyncDisposable
{
    Task InitializeAsync();

    Task<CopilotSession> CreateAnalysisSessionAsync(string model, Context7Config? context7 = null, IEnumerable<AIFunction>? tools = null);

    Task<CopilotSession> CreatePlanningSessionAsync(string model);

    Task<CopilotSession> CreateExecutionSessionAsync(string model, IEnumerable<AIFunction>? tools = null);

    Task<string> SendAndWaitAsync(CopilotSession session, string prompt, List<UserMessageDataAttachmentsItem>? attachments = null);

    Task DisposeSessionAsync(CopilotSession session);
}
