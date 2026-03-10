namespace DemoFresh.Models;

public record DriftFinding(
    string Description,
    DriftSeverity Severity,
    IReadOnlyList<string> AffectedFiles,
    string SuggestedFix,
    string Category);

public enum DriftSeverity
{
    Low,
    Medium,
    High,
    Critical
}
