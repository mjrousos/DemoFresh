namespace DemoFresh.Models;

public record AnalysisReport(
    string RepoUrl,
    string Branch,
    DateTimeOffset AnalyzedAt,
    IReadOnlyList<DemoAnalysis> Demos);

public record DemoAnalysis(
    Demo Demo,
    IReadOnlyList<DriftFinding> Findings,
    string? Plan,
    ActionResult? Action);
