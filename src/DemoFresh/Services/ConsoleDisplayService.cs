using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace DemoFresh.Services;

/// <summary>
/// Renders styled console output for Copilot session activity using Spectre.Console.
/// Maintains display state and re-renders a bordered panel on each state change.
/// </summary>
public sealed class ConsoleDisplayService : IConsoleDisplay
{
    private readonly IAnsiConsole _console;
    private readonly Lock _lock = new();

    private string? _currentSessionId;
    private string? _currentIntent;
    private readonly List<ToolCallEntry> _toolCalls = [];

    private const int MaxToolCallDisplayLength = 80;

    public ConsoleDisplayService(IAnsiConsole console)
    {
        _console = console;
    }

    public void ShowIdle()
    {
        lock (_lock)
        {
            _currentSessionId = null;
            _currentIntent = null;
            _toolCalls.Clear();
        }

        Render();
    }

    public void OnSessionStart(string sessionId)
    {
        lock (_lock)
        {
            _currentSessionId = sessionId;
            _currentIntent = null;
            _toolCalls.Clear();
        }

        Render();
    }

    public void OnIntentChanged(string intent)
    {
        lock (_lock)
        {
            _currentIntent = intent;
            _toolCalls.Clear();
        }

        Render();
    }

    public void OnToolCallStarted(string toolName, string? toolArgs = null)
    {
        lock (_lock)
        {
            var argsSummary = SummarizeArgs(toolArgs);
            _toolCalls.Add(new ToolCallEntry(toolName, argsSummary, IsCompleted: false));
        }

        Render();
    }

    public void OnToolCallCompleted(string toolName)
    {
        lock (_lock)
        {
            // Update the most recent matching running entry
            for (var i = _toolCalls.Count - 1; i >= 0; i--)
            {
                if (_toolCalls[i].ToolName == toolName && !_toolCalls[i].IsCompleted)
                {
                    _toolCalls[i] = _toolCalls[i] with { IsCompleted = true };
                    break;
                }
            }
        }

        Render();
    }

    public void OnSessionEnd()
    {
        lock (_lock)
        {
            _currentSessionId = null;
            _currentIntent = null;
            _toolCalls.Clear();
        }

        Render();
    }

    public void DisplaySummary(string summary)
    {
        _console.Clear();
        var panel = new Panel(Markup.Escape(summary))
        {
            Header = new PanelHeader("Analysis Summary"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };
        _console.Write(panel);
        _console.WriteLine();
    }

    private void Render()
    {
        _console.Clear();

        string? sessionId;
        string? intent;
        List<ToolCallEntry> toolCallsCopy;

        lock (_lock)
        {
            sessionId = _currentSessionId;
            intent = _currentIntent;
            toolCallsCopy = [.. _toolCalls];
        }

        if (sessionId is null)
        {
            _console.MarkupLine("[grey]Waiting for session...[/]");
            return;
        }

        var content = BuildSessionContent(intent, toolCallsCopy);
        var panel = new Panel(content)
        {
            Header = new PanelHeader($" Session: {Truncate(sessionId, 40)} "),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1),
            Expand = true
        };

        _console.Write(panel);
    }

    private static IRenderable BuildSessionContent(string? intent, List<ToolCallEntry> toolCalls)
    {
        var rows = new List<IRenderable>();

        if (intent is not null)
        {
            rows.Add(new Markup($"[bold]Intent: {Markup.Escape(intent)}[/]"));
        }
        else
        {
            rows.Add(new Markup("[grey italic]No intent reported yet[/]"));
        }

        rows.Add(Text.Empty);

        foreach (var tc in toolCalls)
        {
            var status = tc.IsCompleted ? "[green](completed)[/]" : "[yellow](running...)[/]";
            var name = Markup.Escape(tc.ToolName);
            var argsDisplay = tc.ArgsSummary is not null
                ? $" [silver]{Markup.Escape(tc.ArgsSummary)}[/]"
                : string.Empty;
            rows.Add(new Markup($"  [white]↳ {name}[/]{argsDisplay} {status}"));
        }

        return new Rows(rows);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength - 3), "...");
    }

    /// <summary>
    /// Extracts the intent value from report_intent tool args JSON.
    /// Returns null if parsing fails.
    /// </summary>
    public static string? ParseIntentFromArgs(string? toolArgs)
    {
        if (string.IsNullOrEmpty(toolArgs))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(toolArgs);
            if (doc.RootElement.TryGetProperty("intent", out var intentElement))
            {
                return intentElement.GetString();
            }
        }
        catch (JsonException)
        {
            // Malformed JSON — ignore
        }

        return null;
    }

    private const int MaxArgsSummaryLength = 60;

    /// <summary>
    /// Produces a short, single-line summary of tool arguments for display.
    /// </summary>
    public static string? SummarizeArgs(string? toolArgs)
    {
        if (string.IsNullOrWhiteSpace(toolArgs))
        {
            return null;
        }

        // Collapse whitespace/newlines to a single line
        var oneLine = toolArgs.ReplaceLineEndings(" ").Trim();
        return Truncate(oneLine, MaxArgsSummaryLength);
    }

    private sealed record ToolCallEntry(string ToolName, string? ArgsSummary, bool IsCompleted);
}
