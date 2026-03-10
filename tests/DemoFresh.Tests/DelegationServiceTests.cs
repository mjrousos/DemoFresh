using DemoFresh.Models;
using DemoFresh.Services;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DemoFresh.Tests;

public class DelegationServiceTests
{
    private readonly Mock<ICopilotSessionManager> _sessionManagerMock = new();
    private readonly DelegationService _sut;
    private string? _capturedPrompt;

    public DelegationServiceTests()
    {
        _sut = new DelegationService(_sessionManagerMock.Object, NullLogger<DelegationService>.Instance);
    }

    [Fact]
    public async Task DelegateToAgent_Success_ReturnsDelegatedResult()
    {
        SetupExecutionSession("Execution completed successfully");

        var demo = TestDataHelpers.CreateTestDemo();
        var result = await _sut.DelegateToAgentAsync(demo, "Fix all issues", "gpt-5");

        Assert.Equal(ActionResultType.Delegated, result.Type);
        Assert.Equal("Execution completed successfully", result.DelegationConfirmation);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task DelegateToAgent_SendsDelegationPrompt()
    {
        SetupExecutionSession("Done");

        var demo = TestDataHelpers.CreateTestDemo();
        var plan = "Step 1: Update packages\nStep 2: Fix APIs";
        await _sut.DelegateToAgentAsync(demo, plan, "gpt-5");

        Assert.NotNull(_capturedPrompt);
        Assert.StartsWith("& ", _capturedPrompt);
        Assert.Contains(plan, _capturedPrompt);
    }

    [Fact]
    public async Task DelegateToAgent_IncludesDemoName()
    {
        SetupExecutionSession("Done");

        var demo = TestDataHelpers.CreateTestDemo("SpecialDemo");
        await _sut.DelegateToAgentAsync(demo, "Some plan", "gpt-5");

        Assert.NotNull(_capturedPrompt);
        Assert.Contains("SpecialDemo", _capturedPrompt);
    }

    [Fact]
    public async Task DelegateToAgent_SessionManagerThrows_ReturnsFailedResult()
    {
        _sessionManagerMock
            .Setup(m => m.CreateExecutionSessionAsync(It.IsAny<string>(), It.IsAny<IEnumerable<AIFunction>?>()))
            .ReturnsAsync((CopilotSession)null!);

        _sessionManagerMock
            .Setup(m => m.SendAndWaitAsync(It.IsAny<CopilotSession>(), It.IsAny<string>(), It.IsAny<List<UserMessageDataAttachmentsItem>?>()))
            .ThrowsAsync(new InvalidOperationException("Session timed out"));

        var demo = TestDataHelpers.CreateTestDemo();
        var result = await _sut.DelegateToAgentAsync(demo, "Some plan", "gpt-5");

        Assert.Equal(ActionResultType.Failed, result.Type);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Session timed out", result.ErrorMessage);
        Assert.Null(result.DelegationConfirmation);
    }

    [Fact]
    public async Task DelegateToAgent_CreatesExecutionSession()
    {
        SetupExecutionSession("Done");

        var demo = TestDataHelpers.CreateTestDemo();
        await _sut.DelegateToAgentAsync(demo, "Some plan", "gpt-5");

        _sessionManagerMock.Verify(
            m => m.CreateExecutionSessionAsync("gpt-5", It.IsAny<IEnumerable<AIFunction>?>()),
            Times.Once);
    }

    private void SetupExecutionSession(string response)
    {
        _sessionManagerMock
            .Setup(m => m.CreateExecutionSessionAsync(It.IsAny<string>(), It.IsAny<IEnumerable<AIFunction>?>()))
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
