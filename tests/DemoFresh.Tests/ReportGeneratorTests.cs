using DemoFresh.Models;
using DemoFresh.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DemoFresh.Tests;

public class ReportGeneratorTests
{
    private readonly ReportGenerator _sut = new(NullLogger<ReportGenerator>.Instance);

    [Fact]
    public void GenerateHtmlReport_WithFindings_ContainsRepoUrl()
    {
        var report = TestDataHelpers.CreateTestReport();

        var html = _sut.GenerateHtmlReport(report);

        Assert.Contains(report.RepoUrl, html);
    }

    [Fact]
    public void GenerateHtmlReport_WithFindings_ContainsSeverityColors()
    {
        var report = TestDataHelpers.CreateTestReport();

        var html = _sut.GenerateHtmlReport(report);

        // The report should contain severity color codes used in the badge table
        Assert.Contains("#d73a49", html); // Critical
        Assert.Contains("#e36209", html); // High
        Assert.Contains("#dbab09", html); // Medium
        Assert.Contains("#28a745", html); // Low
    }

    [Fact]
    public void GenerateHtmlReport_WithPrAction_ContainsPrUrl()
    {
        var report = TestDataHelpers.CreateTestReport();

        var html = _sut.GenerateHtmlReport(report);

        Assert.Contains("https://github.com/test/repo/pull/1", html);
    }

    [Fact]
    public void GenerateHtmlReport_EmptyReport_NoFindings()
    {
        var report = TestDataHelpers.CreateTestReport(findingCount: 0);

        var html = _sut.GenerateHtmlReport(report);

        Assert.False(string.IsNullOrEmpty(html));
        Assert.Contains("No findings", html);
    }

    [Fact]
    public void GenerateConsoleSummary_WithFindings_ContainsDemoName()
    {
        var report = TestDataHelpers.CreateTestReport();

        var summary = _sut.GenerateConsoleSummary(report);

        Assert.Contains("TestDemo", summary);
    }

    [Fact]
    public void GenerateConsoleSummary_WithNoAction_ShowsNoActionNeeded()
    {
        var demo = TestDataHelpers.CreateTestDemo();
        var demoAnalysis = new DemoAnalysis(
            demo,
            [],
            Plan: null,
            Action: new ActionResult(ActionResultType.NoActionNeeded, null, null, null));
        var report = new AnalysisReport(
            "https://github.com/test/repo",
            "main",
            DateTimeOffset.UtcNow,
            [demoAnalysis]);

        var summary = _sut.GenerateConsoleSummary(report);

        Assert.Contains("No action needed", summary);
    }
}
