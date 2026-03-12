namespace DemoFresh.Services;

/// <summary>
/// Manages styled console output for Copilot session activity.
/// Replaces verbose Serilog console logs with a scoped, intent-driven display.
/// </summary>
public interface IConsoleDisplay
{
    /// <summary>
    /// Shows an idle/waiting message when no session is active.
    /// </summary>
    void ShowIdle();

    /// <summary>
    /// Called when a new Copilot session starts. Clears previous output and displays session context.
    /// </summary>
    void OnSessionStart(string sessionId);

    /// <summary>
    /// Called when the agent's intent changes (via report_intent tool).
    /// Prominently displays the new intent and clears any previous tool call logs.
    /// </summary>
    void OnIntentChanged(string intent);

    /// <summary>
    /// Called when a tool invocation begins. Displayed with lesser emphasis below the current intent.
    /// </summary>
    void OnToolCallStarted(string toolName, string? toolArgs = null);

    /// <summary>
    /// Called when a tool invocation completes. Updates the tool's status in the display.
    /// </summary>
    void OnToolCallCompleted(string toolName);

    /// <summary>
    /// Called when the current session ends. Clears session output and returns to idle state.
    /// </summary>
    void OnSessionEnd();

    /// <summary>
    /// Displays a final analysis summary (e.g., from ReportGenerator) in a styled panel.
    /// </summary>
    void DisplaySummary(string summary);
}
