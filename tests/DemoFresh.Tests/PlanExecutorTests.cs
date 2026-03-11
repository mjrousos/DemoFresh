using DemoFresh.Models;
using DemoFresh.Services;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DemoFresh.Tests;

public class PlanExecutorTests
{
    private readonly Mock<ICopilotSessionManager> _sessionManagerMock = new();
    private readonly PlanExecutor _sut;
    private string? _capturedPrompt;

    public PlanExecutorTests()
    {
        _sut = new PlanExecutor(_sessionManagerMock.Object, NullLogger<PlanExecutor>.Instance);
    }

    [Fact]
    public async Task GeneratePlanAsync_SendsPromptWithDemoDetails()
    {
        SetupPlanningSession("Some plan");

        var demo = TestDataHelpers.CreateTestDemo("MySpecialDemo");
        var findings = new[] { TestDataHelpers.CreateTestFinding() };

        await _sut.GeneratePlanAsync(demo, findings);

        Assert.NotNull(_capturedPrompt);
        Assert.Contains("MySpecialDemo", _capturedPrompt);
        Assert.Contains(findings[0].Description, _capturedPrompt);
    }

    [Fact]
    public async Task GeneratePlanAsync_ReturnsPlanFromSession()
    {
        SetupPlanningSession("Step 1: Do X");

        var demo = TestDataHelpers.CreateTestDemo();
        var findings = new[] { TestDataHelpers.CreateTestFinding() };

        var result = await _sut.GeneratePlanAsync(demo, findings);

        Assert.Equal("Step 1: Do X", result);
    }

    [Fact]
    public async Task GeneratePlanAsync_MultipleFindingsIncludesAll()
    {
        SetupPlanningSession("Plan for all");

        var demo = TestDataHelpers.CreateTestDemo();
        var findings = new[]
        {
            TestDataHelpers.CreateTestFinding(DriftSeverity.Low),
            TestDataHelpers.CreateTestFinding(DriftSeverity.High),
            TestDataHelpers.CreateTestFinding(DriftSeverity.Critical)
        };

        await _sut.GeneratePlanAsync(demo, findings);

        Assert.NotNull(_capturedPrompt);
        Assert.Contains("Low", _capturedPrompt);
        Assert.Contains("High", _capturedPrompt);
        Assert.Contains("Critical", _capturedPrompt);
        Assert.Contains(findings[0].Description, _capturedPrompt);
        Assert.Contains(findings[1].Description, _capturedPrompt);
        Assert.Contains(findings[2].Description, _capturedPrompt);
    }

    [Fact]
    public async Task ExecutePlanAsync_SendsPromptWithWorkingDirectory()
    {
        SetupExecutionSession("Done");

        await _sut.ExecutePlanAsync("Fix things", "/tmp/my-repo");

        Assert.NotNull(_capturedPrompt);
        Assert.Contains("/tmp/my-repo", _capturedPrompt);
    }

    [Fact]
    public async Task ExecutePlanAsync_SendsPromptWithPlan()
    {
        SetupExecutionSession("Done");

        var planText = "Step 1: Update Program.cs\nStep 2: Run tests";
        await _sut.ExecutePlanAsync(planText, "/tmp/repo");

        Assert.NotNull(_capturedPrompt);
        Assert.Contains(planText, _capturedPrompt);
    }

    [Fact]
    public async Task GeneratePlanAsync_CreatesPlanningSession()
    {
        SetupPlanningSession("Plan");

        var demo = TestDataHelpers.CreateTestDemo();
        var findings = new[] { TestDataHelpers.CreateTestFinding() };

        await _sut.GeneratePlanAsync(demo, findings);

        _sessionManagerMock.Verify(
            m => m.CreatePlanningSessionAsync(),
            Times.Once);
    }

    [Fact]
    public async Task ExecutePlanAsync_CreatesExecutionSession()
    {
        SetupExecutionSession("Done");

        await _sut.ExecutePlanAsync("Fix things", "/tmp/repo");

        _sessionManagerMock.Verify(
            m => m.CreateExecutionSessionAsync(),
            Times.Once);
    }

    private void SetupPlanningSession(string response)
    {
        _sessionManagerMock
            .Setup(m => m.CreatePlanningSessionAsync())
            .ReturnsAsync((CopilotSession)null!);

        _sessionManagerMock
            .Setup(m => m.SendAndWaitAsync(It.IsAny<CopilotSession>(), It.IsAny<string>(), It.IsAny<List<UserMessageDataAttachmentsItem>?>()))
            .Callback<CopilotSession, string, List<UserMessageDataAttachmentsItem>?>((_, prompt, _) => _capturedPrompt = prompt)
            .ReturnsAsync(response);

        _sessionManagerMock
            .Setup(m => m.DisposeSessionAsync(It.IsAny<CopilotSession>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupExecutionSession(string response)
    {
        _sessionManagerMock
            .Setup(m => m.CreateExecutionSessionAsync())
            .ReturnsAsync((CopilotSession)null!);

        _sessionManagerMock
            .Setup(m => m.SendAndWaitAsync(It.IsAny<CopilotSession>(), It.IsAny<string>(), It.IsAny<List<UserMessageDataAttachmentsItem>?>()))
            .Callback<CopilotSession, string, List<UserMessageDataAttachmentsItem>?>((_, prompt, _) => _capturedPrompt = prompt)
            .ReturnsAsync(response);

        _sessionManagerMock
            .Setup(m => m.DisposeSessionAsync(It.IsAny<CopilotSession>()))
            .Returns(Task.CompletedTask);
    }
}
