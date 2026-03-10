using DemoFresh.Models;

namespace DemoFresh.Services;

public interface IRepoService : IAsyncDisposable
{
    Task<RepoContents> CloneAndEnumerateAsync(string repoUrl, string? branch = null, CancellationToken ct = default);
    Task CleanupAsync(string workingDirectory);
}
