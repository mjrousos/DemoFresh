using DemoFresh.Models;

namespace DemoFresh.Services;

public interface IPrService
{
    Task<ActionResult> CreatePullRequestAsync(Demo demo, string plan, string workingDirectory, CancellationToken ct = default);
}
