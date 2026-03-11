using DemoFresh.Models;

namespace DemoFresh.Services;

public interface IPlanExecutor
{
    Task<string> GeneratePlanAsync(Demo demo, IReadOnlyList<DriftFinding> findings, CancellationToken ct = default);
    Task ExecutePlanAsync(string plan, string workingDirectory, CancellationToken ct = default);
}
