using System.Text.Json;
using DemoFresh.Models;

namespace DemoFresh.Tests;

public class ModelSerializationTests
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Demo_RoundTripsJson()
    {
        var demo = TestDataHelpers.CreateTestDemo();

        var json = JsonSerializer.Serialize(demo);
        var deserialized = JsonSerializer.Deserialize<Demo>(json, DefaultOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(demo.Name, deserialized!.Name);
        Assert.Equal(demo.Description, deserialized.Description);
        Assert.Equal(demo.Concepts, deserialized.Concepts);
        Assert.Equal(demo.FilePaths, deserialized.FilePaths);
    }

    [Fact]
    public void DriftFinding_RoundTripsJson()
    {
        var finding = TestDataHelpers.CreateTestFinding(DriftSeverity.High);

        var json = JsonSerializer.Serialize(finding);
        var deserialized = JsonSerializer.Deserialize<DriftFinding>(json, DefaultOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(finding.Description, deserialized!.Description);
        Assert.Equal(DriftSeverity.High, deserialized.Severity);
        Assert.Equal(finding.AffectedFiles, deserialized.AffectedFiles);
        Assert.Equal(finding.SuggestedFix, deserialized.SuggestedFix);
        Assert.Equal(finding.Category, deserialized.Category);
    }

    [Fact]
    public void AnalysisReport_RoundTripsJson()
    {
        var report = TestDataHelpers.CreateTestReport(findingCount: 2);

        var json = JsonSerializer.Serialize(report);
        var deserialized = JsonSerializer.Deserialize<AnalysisReport>(json, DefaultOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(report.RepoUrl, deserialized!.RepoUrl);
        Assert.Equal(report.Branch, deserialized.Branch);
        Assert.Single(deserialized.Demos);
        Assert.Equal(2, deserialized.Demos[0].Findings.Count);
    }

    [Fact]
    public void DriftFinding_DeserializeFromLlmStyle()
    {
        var json = """
        {
            "description": "Using deprecated API",
            "severity": "High",
            "affectedFiles": ["src/Program.cs"],
            "suggestedFix": "Migrate to new API",
            "category": "Deprecated API"
        }
        """;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        var finding = JsonSerializer.Deserialize<DriftFinding>(json, options);

        Assert.NotNull(finding);
        Assert.Equal("Using deprecated API", finding!.Description);
        Assert.Equal(DriftSeverity.High, finding.Severity);
        var file = Assert.Single(finding.AffectedFiles);
        Assert.Equal("src/Program.cs", file);
        Assert.Equal("Migrate to new API", finding.SuggestedFix);
        Assert.Equal("Deprecated API", finding.Category);
    }
}
