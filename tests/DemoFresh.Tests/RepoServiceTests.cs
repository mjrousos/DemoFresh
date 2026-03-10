using DemoFresh.Models;
using DemoFresh.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DemoFresh.Tests;

public class RepoServiceTests
{
    private readonly Mock<IProcessRunner> _processRunnerMock = new();
    private readonly RepoService _sut;

    public RepoServiceTests()
    {
        _sut = new RepoService(_processRunnerMock.Object, NullLogger<RepoService>.Instance);
    }

    [Fact]
    public async Task CloneAndEnumerate_CallsGitClone()
    {
        var repoUrl = "https://github.com/test/repo.git";
        string? capturedArgs = null;

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.Contains("clone")), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, CancellationToken>((cmd, args, dir, ct) => capturedArgs = args)
            .ReturnsAsync(new ProcessResult(0, "", ""));

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.Contains("rev-parse")), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "main\n", ""));

        RepoContents? result = null;
        try
        {
            result = await _sut.CloneAndEnumerateAsync(repoUrl);
            Assert.NotNull(capturedArgs);
            Assert.Contains(repoUrl, capturedArgs);
            Assert.Contains("clone", capturedArgs);
        }
        finally
        {
            if (result != null) await _sut.CleanupAsync(result.WorkingDirectory);
        }
    }

    [Fact]
    public async Task CloneAndEnumerate_WithBranch_IncludesBranchInArgs()
    {
        var repoUrl = "https://github.com/test/repo.git";
        string? capturedArgs = null;

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.Contains("clone")), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, CancellationToken>((cmd, args, dir, ct) => capturedArgs = args)
            .ReturnsAsync(new ProcessResult(0, "", ""));

        RepoContents? result = null;
        try
        {
            result = await _sut.CloneAndEnumerateAsync(repoUrl, branch: "main");
            Assert.NotNull(capturedArgs);
            Assert.Contains("--branch main", capturedArgs);
        }
        finally
        {
            if (result != null) await _sut.CleanupAsync(result.WorkingDirectory);
        }
    }

    [Fact]
    public async Task CloneAndEnumerate_CloneFailure_Throws()
    {
        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(1, "", "error: clone failed"));

        var act = () => _sut.CloneAndEnumerateAsync("https://github.com/test/repo.git");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("exit code 1", ex.Message);
    }

    [Fact]
    public async Task CloneAndEnumerate_InvalidUrl_ThrowsOrReturnsError()
    {
        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(128, string.Empty, "fatal: repository not found"));

        var act = () => _sut.CloneAndEnumerateAsync("https://not-a-real-host.invalid/repo.git");

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task CloneAndEnumerate_GetsCurrentBranch()
    {
        var repoUrl = "https://github.com/test/repo.git";

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.Contains("clone")), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.Contains("rev-parse")), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "develop\n", ""));

        RepoContents? result = null;
        try
        {
            result = await _sut.CloneAndEnumerateAsync(repoUrl);
            Assert.Equal("develop", result.Branch);
        }
        finally
        {
            if (result != null) await _sut.CleanupAsync(result.WorkingDirectory);
        }
    }

    [Fact]
    public async Task CloneAndEnumerate_EnumeratesSourceFiles()
    {
        var repoUrl = "https://github.com/test/repo.git";

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.Contains("clone")), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, CancellationToken>((cmd, args, dir, ct) =>
            {
                var targetDir = args.Split(' ').Last();
                File.WriteAllText(Path.Combine(targetDir, "Program.cs"), "class Program {}");
                File.WriteAllText(Path.Combine(targetDir, "README.md"), "# Readme");
                File.WriteAllText(Path.Combine(targetDir, "app.dll"), "binary content");
            })
            .ReturnsAsync(new ProcessResult(0, "", ""));

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.Contains("rev-parse")), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "main\n", ""));

        RepoContents? result = null;
        try
        {
            result = await _sut.CloneAndEnumerateAsync(repoUrl);
            Assert.Equal(2, result.Files.Count);
            Assert.Contains(result.Files, f => f.Extension == ".cs");
            Assert.Contains(result.Files, f => f.Extension == ".md");
            Assert.DoesNotContain(result.Files, f => f.Extension == ".dll");
        }
        finally
        {
            if (result != null) await _sut.CleanupAsync(result.WorkingDirectory);
        }
    }

    [Fact]
    public async Task CloneAndEnumerate_SkipsLargeFiles()
    {
        var repoUrl = "https://github.com/test/repo.git";

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.Contains("clone")), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, CancellationToken>((cmd, args, dir, ct) =>
            {
                var targetDir = args.Split(' ').Last();
                File.WriteAllText(Path.Combine(targetDir, "Small.cs"), "small file");
                File.WriteAllBytes(Path.Combine(targetDir, "Large.cs"), new byte[101 * 1024]);
            })
            .ReturnsAsync(new ProcessResult(0, "", ""));

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.Contains("rev-parse")), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "main\n", ""));

        RepoContents? result = null;
        try
        {
            result = await _sut.CloneAndEnumerateAsync(repoUrl);
            var file = Assert.Single(result.Files);
            Assert.Equal("Small.cs", file.RelativePath);
        }
        finally
        {
            if (result != null) await _sut.CleanupAsync(result.WorkingDirectory);
        }
    }

    [Fact]
    public async Task CloneAndEnumerate_SkipsExcludedDirectories()
    {
        var repoUrl = "https://github.com/test/repo.git";

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.Contains("clone")), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, CancellationToken>((cmd, args, dir, ct) =>
            {
                var targetDir = args.Split(' ').Last();

                File.WriteAllText(Path.Combine(targetDir, "App.cs"), "class App {}");

                var nodeModulesDir = Path.Combine(targetDir, "node_modules");
                Directory.CreateDirectory(nodeModulesDir);
                File.WriteAllText(Path.Combine(nodeModulesDir, "package.json"), "{}");

                var gitDir = Path.Combine(targetDir, ".git");
                Directory.CreateDirectory(gitDir);
                File.WriteAllText(Path.Combine(gitDir, "config.ini"), "[core]");
            })
            .ReturnsAsync(new ProcessResult(0, "", ""));

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.Contains("rev-parse")), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "main\n", ""));

        RepoContents? result = null;
        try
        {
            result = await _sut.CloneAndEnumerateAsync(repoUrl);
            var file = Assert.Single(result.Files);
            Assert.Equal("App.cs", file.RelativePath);
        }
        finally
        {
            if (result != null) await _sut.CleanupAsync(result.WorkingDirectory);
        }
    }

    [Fact]
    public async Task Cleanup_ExistingDirectory_DeletesIt()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"demofresh-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "test.txt"), "test content");

        await _sut.CleanupAsync(tempDir);

        Assert.False(Directory.Exists(tempDir));
    }

    [Fact]
    public async Task Cleanup_NonexistentDirectory_DoesNotThrow()
    {
        var nonexistentDir = Path.Combine(Path.GetTempPath(), $"demofresh-nonexistent-{Guid.NewGuid():N}");

        var exception = await Record.ExceptionAsync(() => _sut.CleanupAsync(nonexistentDir));
        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_CleansUpTrackedDirectories()
    {
        var repoUrl = "https://github.com/test/repo.git";

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.Contains("clone")), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.Contains("rev-parse")), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "main\n", ""));

        // Clone creates a tracked temp directory
        var result = await _sut.CloneAndEnumerateAsync(repoUrl);
        Assert.True(Directory.Exists(result.WorkingDirectory));

        // Dispose should clean it up without explicit CleanupAsync call
        await _sut.DisposeAsync();

        Assert.False(Directory.Exists(result.WorkingDirectory));
    }

    [Fact]
    public async Task DisposeAsync_SkipsAlreadyCleanedDirectories()
    {
        var repoUrl = "https://github.com/test/repo.git";

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.Contains("clone")), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.Contains("rev-parse")), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "main\n", ""));

        var result = await _sut.CloneAndEnumerateAsync(repoUrl);
        await _sut.CleanupAsync(result.WorkingDirectory);
        Assert.False(Directory.Exists(result.WorkingDirectory));

        // Dispose should not throw even though the directory was already cleaned
        var exception = await Record.ExceptionAsync(() => _sut.DisposeAsync().AsTask());
        Assert.Null(exception);
    }
}
