using DemoFresh.Configuration;
using DemoFresh.Models;
using DemoFresh.Services;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DemoFresh.Tests.IntegrationTests;

[Trait("Category", "Integration")]
public class OrchestratorIntegrationTests
{
    [Fact]
    public async Task Orchestrator_WithMockedServices_RunsToCompletion()
    {
        // Arrange
        var repoServiceMock = new Mock<IRepoService>();
        var sessionManagerMock = new Mock<ICopilotSessionManager>();
        var driftAnalyzerMock = new Mock<IDriftAnalyzer>();
        var planExecutorMock = new Mock<IPlanExecutor>();
        var prServiceMock = new Mock<IPrService>();
        var reportGeneratorMock = new Mock<IReportGenerator>();
        var lifetimeMock = new Mock<IHostApplicationLifetime>();

        var options = Options.Create(new DemoFreshOptions
        {
            Repos = [new RepoConfig { Url = "https://github.com/test/repo" }],
            ActionMode = ActionMode.CreatePR,
            Model = "gpt-5"
        });

        var repoContents = new RepoContents(
            "/tmp/test",
            "https://github.com/test/repo",
            "main",
            [new RepoFile("src/Program.cs", "Console.WriteLine(\"Hello\");", ".cs")]);

        var testDemo = TestDataHelpers.CreateTestDemo();
        var testFinding = TestDataHelpers.CreateTestFinding();

        // Setup mocks
        repoServiceMock
            .Setup(r => r.CloneAndEnumerateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(repoContents);

        repoServiceMock
            .Setup(r => r.CleanupAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        sessionManagerMock
            .Setup(s => s.InitializeAsync())
            .Returns(Task.CompletedTask);

        sessionManagerMock
            .Setup(s => s.CreateAnalysisSessionAsync(It.IsAny<string?>()))
            .ReturnsAsync((CopilotSession)null!);

        sessionManagerMock
            .Setup(s => s.SendAndWaitAsync(It.IsAny<CopilotSession>(), It.IsAny<string>(), It.IsAny<List<UserMessageDataAttachmentsItem>?>()))
            .ReturnsAsync("Email sent successfully");

        sessionManagerMock
            .Setup(s => s.DisposeSessionAsync(It.IsAny<CopilotSession>()))
            .Returns(Task.CompletedTask);

        driftAnalyzerMock
            .Setup(d => d.IdentifyDemosAsync(It.IsAny<RepoContents>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Demo> { testDemo });

        driftAnalyzerMock
            .Setup(d => d.AnalyzeDriftAsync(It.IsAny<Demo>(), It.IsAny<RepoContents>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DriftFinding> { testFinding });

        planExecutorMock
            .Setup(p => p.GeneratePlanAsync(It.IsAny<Demo>(), It.IsAny<IReadOnlyList<DriftFinding>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Step 1: Fix the issue");

        planExecutorMock
            .Setup(p => p.ExecutePlanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        prServiceMock
            .Setup(p => p.CreatePullRequestAsync(It.IsAny<Demo>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActionResult(ActionResultType.PrCreated, "https://github.com/test/repo/pull/1", null, null));

        reportGeneratorMock
            .Setup(r => r.GenerateConsoleSummary(It.IsAny<AnalysisReport>()))
            .Returns("Summary output");

        reportGeneratorMock
            .Setup(r => r.GenerateHtmlReport(It.IsAny<AnalysisReport>()))
            .Returns("<html>Report</html>");

        // Setup lifetime to use a real CancellationTokenSource
        var cts = new CancellationTokenSource();
        lifetimeMock.Setup(l => l.StopApplication()).Callback(() => cts.Cancel());

        var consoleDisplayMock = new Mock<IConsoleDisplay>();

        var orchestrator = new AnalysisOrchestrator(
            repoServiceMock.Object,
            sessionManagerMock.Object,
            driftAnalyzerMock.Object,
            planExecutorMock.Object,
            prServiceMock.Object,
            reportGeneratorMock.Object,
            consoleDisplayMock.Object,
            options,
            NullLogger<AnalysisOrchestrator>.Instance,
            lifetimeMock.Object);

        // Act
        var act = () => orchestrator.StartAsync(cts.Token);

        // Assert - should complete without throwing
        await act();

        // Allow background task to finish
        await Task.Delay(500);

        // Verify services were called in expected order
        sessionManagerMock.Verify(s => s.InitializeAsync(), Times.Once);
        repoServiceMock.Verify(r => r.CloneAndEnumerateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        driftAnalyzerMock.Verify(d => d.IdentifyDemosAsync(It.IsAny<RepoContents>(), It.IsAny<CancellationToken>()), Times.Once);
        driftAnalyzerMock.Verify(d => d.AnalyzeDriftAsync(It.IsAny<Demo>(), It.IsAny<RepoContents>(), It.IsAny<CancellationToken>()), Times.Once);
        planExecutorMock.Verify(p => p.GeneratePlanAsync(It.IsAny<Demo>(), It.IsAny<IReadOnlyList<DriftFinding>>(), It.IsAny<CancellationToken>()), Times.Once);
        prServiceMock.Verify(p => p.CreatePullRequestAsync(It.IsAny<Demo>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        reportGeneratorMock.Verify(r => r.GenerateConsoleSummary(It.IsAny<AnalysisReport>()), Times.Once);
        repoServiceMock.Verify(r => r.CleanupAsync(It.IsAny<string>()), Times.Once);
    }
}
