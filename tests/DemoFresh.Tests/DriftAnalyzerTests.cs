using System.Text.Json;
using DemoFresh.Models;
using DemoFresh.Services;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DemoFresh.Tests;

public class DriftAnalyzerTests
{
    private readonly Mock<ICopilotSessionManager> _sessionManagerMock = new();
    private readonly DriftAnalyzer _sut;

    private static readonly RepoContents TestRepo = new(
        WorkingDirectory: "/tmp/test",
        RepoUrl: "https://github.com/test/repo",
        Branch: "main",
        Files: [new RepoFile("src/Program.cs", "Console.WriteLine(\"Hello\");", ".cs")]);

    public DriftAnalyzerTests()
    {
        _sut = new DriftAnalyzer(_sessionManagerMock.Object, NullLogger<DriftAnalyzer>.Instance);
    }

    [Fact]
    public async Task IdentifyDemos_ValidResponse_ParsesDemos()
    {
        var demos = new[]
        {
            new Demo("Demo1", "First demo", ["C#"], ["src/Program.cs"]),
            new Demo("Demo2", "Second demo", [".NET"], ["src/Helper.cs"])
        };
        var jsonResponse = JsonSerializer.Serialize(demos);

        SetupSessionManagerToReturn(jsonResponse);

        var result = await _sut.IdentifyDemosAsync(TestRepo, "gpt-5");

        Assert.Equal(2, result.Count);
        Assert.Equal("Demo1", result[0].Name);
        Assert.Equal("Demo2", result[1].Name);
    }

    [Fact]
    public async Task IdentifyDemos_InvalidJson_ReturnsFallbackDemo()
    {
        SetupSessionManagerToReturn("This is not valid JSON at all");

        var result = await _sut.IdentifyDemosAsync(TestRepo, "gpt-5");

        Assert.Single(result);
        Assert.Equal("Full Repository", result[0].Name);
    }

    [Fact]
    public async Task AnalyzeDrift_NoFindings_ReturnsEmptyList()
    {
        SetupSessionManagerToReturn("No drift found in this demo.");

        var demo = TestDataHelpers.CreateTestDemo();
        var result = await _sut.AnalyzeDriftAsync(demo, TestRepo, "gpt-5");

        Assert.Empty(result);
    }

    [Fact]
    public async Task AnalyzeDrift_ValidFindings_ParsesCorrectly()
    {
        var findings = new[]
        {
            new DriftFinding("Deprecated API usage", DriftSeverity.High, ["src/Program.cs"], "Use new API", "Deprecated API")
        };
        var jsonResponse = JsonSerializer.Serialize(findings);

        SetupSessionManagerToReturn(jsonResponse);

        var demo = TestDataHelpers.CreateTestDemo();
        var result = await _sut.AnalyzeDriftAsync(demo, TestRepo, "gpt-5");

        Assert.Single(result);
        Assert.Equal("Deprecated API usage", result[0].Description);
        Assert.Equal(DriftSeverity.High, result[0].Severity);
    }

    private void SetupSessionManagerToReturn(string response)
    {
        _sessionManagerMock
            .Setup(m => m.CreateAnalysisSessionAsync(It.IsAny<string>(), It.IsAny<DemoFresh.Configuration.Context7Config?>(), It.IsAny<IEnumerable<Microsoft.Extensions.AI.AIFunction>?>()))
            .ReturnsAsync((CopilotSession)null!);

        _sessionManagerMock
            .Setup(m => m.SendAndWaitAsync(It.IsAny<CopilotSession>(), It.IsAny<string>(), It.IsAny<List<UserMessageDataAttachmentsItem>?>()))
            .ReturnsAsync(response);

        _sessionManagerMock
            .Setup(m => m.DisposeSessionAsync(It.IsAny<CopilotSession>()))
            .Returns(Task.CompletedTask);
    }
}
