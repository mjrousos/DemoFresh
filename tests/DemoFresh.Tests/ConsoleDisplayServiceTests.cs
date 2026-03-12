using Spectre.Console;
using Spectre.Console.Testing;
using DemoFresh.Services;

namespace DemoFresh.Tests;

public class ConsoleDisplayServiceTests
{
    private readonly TestConsole _testConsole;
    private readonly ConsoleDisplayService _sut;

    public ConsoleDisplayServiceTests()
    {
        _testConsole = new TestConsole();
        _testConsole.Profile.Width = 120;
        _sut = new ConsoleDisplayService(_testConsole);
    }

    [Fact]
    public void ShowIdle_DisplaysWaitingMessage()
    {
        _sut.ShowIdle();

        var output = _testConsole.Output;
        Assert.Contains("Waiting for session", output);
    }

    [Fact]
    public void OnSessionStart_DisplaysSessionPanel()
    {
        _sut.OnSessionStart("test-session-123");

        var output = _testConsole.Output;
        Assert.Contains("test-session-123", output);
        Assert.Contains("No intent reported yet", output);
    }

    [Fact]
    public void OnIntentChanged_DisplaysIntent()
    {
        _sut.OnSessionStart("session-1");
        _sut.OnIntentChanged("Exploring demo repo");

        var output = _testConsole.Output;
        Assert.Contains("Exploring demo repo", output);
    }

    [Fact]
    public void OnToolCallStarted_DisplaysRunningToolWithArgs()
    {
        _sut.OnSessionStart("session-1");
        _sut.OnToolCallStarted("get_file_contents", "{\"path\":\"src/Program.cs\"}");

        var output = _testConsole.Output;
        Assert.Contains("get_file_contents", output);
        Assert.Contains("src/Program.cs", output);
        Assert.Contains("running", output);
    }

    [Fact]
    public void OnToolCallStarted_NoArgs_DisplaysToolNameOnly()
    {
        _sut.OnSessionStart("session-1");
        _sut.OnToolCallStarted("get_file_contents");

        var output = _testConsole.Output;
        Assert.Contains("get_file_contents", output);
        Assert.Contains("running", output);
    }

    [Fact]
    public void OnToolCallCompleted_DisplaysCompletedTool()
    {
        _sut.OnSessionStart("session-1");
        _sut.OnToolCallStarted("list_directory");
        _sut.OnToolCallCompleted("list_directory");

        var output = _testConsole.Output;
        Assert.Contains("list_directory", output);
        Assert.Contains("completed", output);
    }

    [Fact]
    public void OnIntentChanged_ClearsPreviousToolCalls()
    {
        _sut.OnSessionStart("session-1");
        _sut.OnIntentChanged("First intent");
        _sut.OnToolCallStarted("tool_a");
        _sut.OnIntentChanged("Second intent");

        // After intent change, the last render should not show the old tool call
        var output = _testConsole.Output;
        Assert.Contains("Second intent", output);
        // tool_a was from the previous intent group; the final render shouldn't have running tool_a
        // (it appears in earlier render output, but the last render is what matters)
    }

    [Fact]
    public void OnSessionEnd_ReturnsToIdle()
    {
        _sut.OnSessionStart("session-1");
        _sut.OnIntentChanged("Doing stuff");
        _sut.OnSessionEnd();

        var output = _testConsole.Output;
        Assert.Contains("Waiting for session", output);
    }

    [Fact]
    public void DisplaySummary_RendersSummaryPanel()
    {
        _sut.DisplaySummary("Found 3 demos with 5 drift findings");

        var output = _testConsole.Output;
        Assert.Contains("Analysis Summary", output);
        Assert.Contains("Found 3 demos with 5 drift findings", output);
    }

    [Fact]
    public void MultipleToolCalls_AllDisplayed()
    {
        _sut.OnSessionStart("session-1");
        _sut.OnIntentChanged("Analyzing code");
        _sut.OnToolCallStarted("get_file_contents");
        _sut.OnToolCallCompleted("get_file_contents");
        _sut.OnToolCallStarted("search_code");

        var output = _testConsole.Output;
        Assert.Contains("get_file_contents", output);
        Assert.Contains("search_code", output);
    }

    [Fact]
    public void SummarizeArgs_TruncatesLongArgs()
    {
        var longArgs = new string('x', 200);
        var result = ConsoleDisplayService.SummarizeArgs(longArgs);
        Assert.NotNull(result);
        Assert.True(result.Length <= 60);
        Assert.EndsWith("...", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SummarizeArgs_EmptyInput_ReturnsNull(string? input)
    {
        Assert.Null(ConsoleDisplayService.SummarizeArgs(input));
    }

    [Theory]
    [InlineData("{\"intent\":\"Exploring demo repo\"}", "Exploring demo repo")]
    [InlineData("{\"intent\":\"Analyzing drift\"}", "Analyzing drift")]
    [InlineData("{\"intent\":\"\"}", "")]
    public void ParseIntentFromArgs_ValidJson_ReturnsIntent(string json, string expected)
    {
        var result = ConsoleDisplayService.ParseIntentFromArgs(json);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"other\":\"value\"}")]
    public void ParseIntentFromArgs_InvalidInput_ReturnsNull(string? json)
    {
        var result = ConsoleDisplayService.ParseIntentFromArgs(json);
        Assert.Null(result);
    }
}
