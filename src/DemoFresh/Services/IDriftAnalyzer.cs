using DemoFresh.Configuration;
using DemoFresh.Models;

namespace DemoFresh.Services;

public interface IDriftAnalyzer
{
    Task<IReadOnlyList<Demo>> IdentifyDemosAsync(RepoContents repo, string model, Context7Config? context7 = null, CancellationToken ct = default);

    Task<IReadOnlyList<DriftFinding>> AnalyzeDriftAsync(Demo demo, RepoContents repo, string model, Context7Config? context7 = null, CancellationToken ct = default);
}
