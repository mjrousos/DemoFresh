using DemoFresh.Models;
using Microsoft.Extensions.Logging;

namespace DemoFresh.Services;

public class RepoService(IProcessRunner processRunner, ILogger<RepoService> logger) : IRepoService
{
    private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".sln", ".json", ".md", ".txt",
        ".yml", ".yaml", ".xml", ".html", ".css", ".js", ".ts",
        ".py", ".java", ".go", ".rs", ".rb", ".php",
        ".sh", ".ps1", ".dockerfile", ".toml", ".cfg", ".ini", ".env"
    };

    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", ".vs", ".idea",
        "packages", "dist", "build", "out", "target",
        "__pycache__", ".mypy_cache", ".pytest_cache",
        "vendor", "coverage", ".next", ".nuget"
    };

    private const long MaxFileSizeBytes = 100 * 1024; // 100KB

    public async Task<RepoContents> CloneAndEnumerateAsync(string repoUrl, string? branch = null, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"demofresh-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        logger.LogInformation("Cloning repository {RepoUrl} into {TempDir}", repoUrl, tempDir);

        var args = branch is not null
            ? $"clone --branch {branch} --depth 1 {repoUrl} {tempDir}"
            : $"clone --depth 1 {repoUrl} {tempDir}";

        var cloneResult = await processRunner.RunAsync("git", args, null, ct);
        if (cloneResult.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git clone failed with exit code {cloneResult.ExitCode}: {cloneResult.StandardError}");
        }

        logger.LogInformation("Clone complete for {RepoUrl}", repoUrl);

        // Determine actual branch name from the cloned repo
        string actualBranch;
        if (branch is not null)
        {
            actualBranch = branch;
        }
        else
        {
            var branchResult = await processRunner.RunAsync("git", "rev-parse --abbrev-ref HEAD", tempDir, ct);
            actualBranch = branchResult.StandardOutput.Trim();
        }

        var files = new List<RepoFile>();
        var skippedCount = 0;

        foreach (var filePath in Directory.EnumerateFiles(tempDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(tempDir, filePath);

            // Skip files in excluded directories
            if (IsInSkippedDirectory(relativePath))
            {
                skippedCount++;
                continue;
            }

            var extension = Path.GetExtension(filePath);

            // Only include files with recognized source extensions
            if (!SourceExtensions.Contains(extension))
            {
                skippedCount++;
                continue;
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxFileSizeBytes)
            {
                logger.LogDebug("Skipping large file {File} ({Size} bytes)", relativePath, fileInfo.Length);
                skippedCount++;
                continue;
            }

            var content = await File.ReadAllTextAsync(filePath, ct);
            files.Add(new RepoFile(relativePath, content, extension));
        }

        logger.LogInformation("Enumerated {FileCount} source files, skipped {SkippedCount} files", files.Count, skippedCount);

        return new RepoContents(tempDir, repoUrl, actualBranch, files);
    }

    public Task CleanupAsync(string workingDirectory)
    {
        try
        {
            if (Directory.Exists(workingDirectory))
            {
                // Reset read-only attributes that git may have set
                foreach (var file in Directory.EnumerateFiles(workingDirectory, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(workingDirectory, recursive: true);
                logger.LogInformation("Cleaned up working directory {Directory}", workingDirectory);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean up working directory {Directory}", workingDirectory);
        }

        return Task.CompletedTask;
    }

    private static bool IsInSkippedDirectory(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => SkippedDirectories.Contains(part));
    }
}
