using DemoFresh.Models;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;

namespace DemoFresh.Services;

public class DelegationService(ICopilotSessionManager sessionManager, ILogger<DelegationService> logger) : IDelegationService
{
    public async Task<ActionResult> DelegateToAgentAsync(Demo demo, string plan, string model, CancellationToken ct = default)
    {
        CopilotSession? session = null;

        try
        {
            logger.LogInformation("Delegating execution for demo '{DemoName}' to coding agent", demo.Name);

            session = await sessionManager.CreateExecutionSessionAsync(model);

            var prompt = $"& Please execute the following plan for demo '{demo.Name}':\n\n{plan}";
            var response = await sessionManager.SendAndWaitAsync(session, prompt);

            logger.LogInformation("Delegation complete for demo '{DemoName}': {Response}", demo.Name, response);
            return new ActionResult(ActionResultType.Delegated, PrUrl: null, DelegationConfirmation: response, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delegate execution for demo '{DemoName}'", demo.Name);
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
}
