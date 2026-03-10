using DemoFresh.Models;
using DemoFresh.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DemoFresh.Tests;

public class PrServiceTests
{
    private readonly Mock<IProcessRunner> _processRunnerMock = new();
    private readonly PrService _sut;

    public PrServiceTests()
    {
        _sut = new PrService(_processRunnerMock.Object, NullLogger<PrService>.Instance);
    }

    [Fact]
    public async Task CreatePr_Success_ReturnsPrCreatedResult()
    {
        var demo = TestDataHelpers.CreateTestDemo();
        var prUrl = "https://github.com/test/repo/pull/42";

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        _processRunnerMock
            .Setup(p => p.RunAsync("gh", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new ProcessResult(0, prUrl, ""));

        var result = await _sut.CreatePullRequestAsync(demo, "Update plan", "/work/dir");

        Assert.Equal(ActionResultType.PrCreated, result.Type);
        Assert.Equal(prUrl, result.PrUrl);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task CreatePr_GeneratesCorrectBranchName()
    {
        var demo = TestDataHelpers.CreateTestDemo("Test Demo");
        string? capturedCheckoutArgs = null;

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.StartsWith("checkout")), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Callback<string, string, string?, CancellationToken, string?>((cmd, args, dir, ct, stdin) => capturedCheckoutArgs = args)
            .ReturnsAsync(new ProcessResult(0, "", ""));

        _processRunnerMock
            .Setup(p => p.RunAsync("gh", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new ProcessResult(0, "https://github.com/test/repo/pull/1", ""));

        await _sut.CreatePullRequestAsync(demo, "plan", "/work/dir");

        var expectedDate = DateTime.UtcNow.ToString("yyyyMMdd");
        Assert.NotNull(capturedCheckoutArgs);
        Assert.Contains($"demofresh/test-demo-{expectedDate}", capturedCheckoutArgs);
    }

    [Fact]
    public async Task CreatePr_CallsGitCheckout_Add_Commit_Push_InOrder()
    {
        var demo = TestDataHelpers.CreateTestDemo();
        var calls = new List<string>();

        _processRunnerMock
            .Setup(p => p.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Callback<string, string, string?, CancellationToken, string?>((cmd, args, dir, ct, stdin) =>
                calls.Add($"{cmd} {args.Split(' ')[0]}"))
            .ReturnsAsync(new ProcessResult(0, "https://github.com/test/repo/pull/1", ""));

        await _sut.CreatePullRequestAsync(demo, "plan", "/work/dir");

        Assert.Equal(5, calls.Count);
        Assert.StartsWith("git checkout", calls[0]);
        Assert.StartsWith("git add", calls[1]);
        Assert.StartsWith("git commit", calls[2]);
        Assert.StartsWith("git push", calls[3]);
        Assert.StartsWith("gh pr", calls[4]);
    }

    [Fact]
    public async Task CreatePr_GitFailure_ReturnsFailedResult()
    {
        var demo = TestDataHelpers.CreateTestDemo();

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.Is<string>(a => a.StartsWith("checkout")), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new ProcessResult(1, "", "error: pathspec did not match"));

        var result = await _sut.CreatePullRequestAsync(demo, "plan", "/work/dir");

        Assert.Equal(ActionResultType.Failed, result.Type);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
        Assert.Null(result.PrUrl);
    }

    [Fact]
    public async Task CreatePr_GhPrCreateFailure_ReturnsFailedResult()
    {
        var demo = TestDataHelpers.CreateTestDemo();

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        _processRunnerMock
            .Setup(p => p.RunAsync("gh", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new ProcessResult(1, "", "error: Could not create PR"));

        var result = await _sut.CreatePullRequestAsync(demo, "plan", "/work/dir");

        Assert.Equal(ActionResultType.Failed, result.Type);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [Fact]
    public async Task CreatePr_ParsesPrUrl_FromGhOutput()
    {
        var demo = TestDataHelpers.CreateTestDemo();
        var expectedUrl = "https://github.com/test/repo/pull/99";

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        _processRunnerMock
            .Setup(p => p.RunAsync("gh", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new ProcessResult(0, $"Creating pull request for branch\n{expectedUrl}\n", ""));

        var result = await _sut.CreatePullRequestAsync(demo, "plan", "/work/dir");

        Assert.Equal(expectedUrl, result.PrUrl);
    }

    [Fact]
    public async Task CreatePr_PassesPlanViaStdin()
    {
        var demo = TestDataHelpers.CreateTestDemo();
        var plan = "Fix\nthe \"bug\"";
        string? capturedGhArgs = null;
        string? capturedStdin = null;

        _processRunnerMock
            .Setup(p => p.RunAsync("git", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        _processRunnerMock
            .Setup(p => p.RunAsync("gh", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Callback<string, string, string?, CancellationToken, string?>((cmd, args, dir, ct, stdin) => { capturedGhArgs = args; capturedStdin = stdin; })
            .ReturnsAsync(new ProcessResult(0, "https://github.com/test/repo/pull/1", ""));

        await _sut.CreatePullRequestAsync(demo, plan, "/work/dir");

        Assert.NotNull(capturedGhArgs);
        Assert.Contains("--body-file -", capturedGhArgs);
        Assert.Equal(plan, capturedStdin);
    }
}
