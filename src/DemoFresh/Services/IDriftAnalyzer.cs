using DemoFresh.Models;

namespace DemoFresh.Services;

public interface IDriftAnalyzer
{
    Task<IReadOnlyList<Demo>> IdentifyDemosAsync(RepoContents repo, string model, CancellationToken ct = default);

    Task<IReadOnlyList<DriftFinding>> AnalyzeDriftAsync(Demo demo, RepoContents repo, string model, CancellationToken ct = default);
}
