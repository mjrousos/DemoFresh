using DemoFresh.Models;

namespace DemoFresh.Tests;

internal static class TestDataHelpers
{
    public static Demo CreateTestDemo(string name = "TestDemo") =>
        new(
            Name: name,
            Description: $"A test demo for {name}",
            Concepts: ["C#", ".NET", "Testing"],
            FilePaths: ["src/Program.cs", "src/Helper.cs"]);

    public static DriftFinding CreateTestFinding(DriftSeverity severity = DriftSeverity.Medium) =>
        new(
            Description: $"Test finding with {severity} severity",
            Severity: severity,
            AffectedFiles: ["src/Program.cs"],
            SuggestedFix: "Update to latest API",
            Category: "Outdated Pattern");

    public static AnalysisReport CreateTestReport(int findingCount = 1)
    {
        var demo = CreateTestDemo();
        var findings = Enumerable.Range(0, findingCount)
            .Select(_ => CreateTestFinding())
            .ToList();

        var action = findingCount > 0
            ? new ActionResult(ActionResultType.PrCreated, "https://github.com/test/repo/pull/1", null, null)
            : new ActionResult(ActionResultType.NoActionNeeded, null, null, null);

        var demoAnalysis = new DemoAnalysis(demo, findings, "Fix the issues", action);

        return new AnalysisReport(
            "https://github.com/test/repo",
            "main",
            DateTimeOffset.UtcNow,
            [demoAnalysis]);
    }
}
