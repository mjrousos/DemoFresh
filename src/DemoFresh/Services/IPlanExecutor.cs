using DemoFresh.Models;

namespace DemoFresh.Services;

public interface IPlanExecutor
{
    Task<string> GeneratePlanAsync(Demo demo, IReadOnlyList<DriftFinding> findings, string model, CancellationToken ct = default);
    Task ExecutePlanAsync(string plan, string workingDirectory, string model, CancellationToken ct = default);
}
