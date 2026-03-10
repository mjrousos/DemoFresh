using DemoFresh.Models;

namespace DemoFresh.Services;

public interface IDelegationService
{
    Task<ActionResult> DelegateToAgentAsync(Demo demo, string plan, string model, CancellationToken ct = default);
}
