using System.Text;
using System.Text.Json;
using DemoFresh.Models;
using Microsoft.Extensions.Logging;

namespace DemoFresh.Services;

public sealed class DriftAnalyzer(
    ICopilotSessionManager sessionManager,
    ILogger<DriftAnalyzer> logger) : IDriftAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<Demo>> IdentifyDemosAsync(
        RepoContents repo, string model, CancellationToken ct = default)
    {
        var session = await sessionManager.CreateAnalysisSessionAsync(model);
        try
        {
            var prompt = BuildIdentifyDemosPrompt(repo);
            var response = await sessionManager.SendAndWaitAsync(session, prompt);
            var json = ExtractJsonArray(response);

            if (json is null)
            {
                logger.LogWarning("Failed to parse demos JSON from Copilot response; returning single demo with all files");
                return [CreateFallbackDemo(repo)];
            }

            try
            {
                var demos = JsonSerializer.Deserialize<List<Demo>>(json, JsonOptions);
                if (demos is null or { Count: 0 })
                {
                    logger.LogWarning("Deserialized demos list was null or empty; returning single demo with all files");
                    return [CreateFallbackDemo(repo)];
                }

                logger.LogInformation("Identified {DemoCount} demos in repository {Repo}", demos.Count, repo.RepoUrl);
                return demos;
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize demos JSON; returning single demo with all files");
                return [CreateFallbackDemo(repo)];
            }
        }
        finally
        {
            await sessionManager.DisposeSessionAsync(session);
        }
    }

    public async Task<IReadOnlyList<DriftFinding>> AnalyzeDriftAsync(
        Demo demo, RepoContents repo, string model, CancellationToken ct = default)
    {
        var session = await sessionManager.CreateAnalysisSessionAsync(model);
        try
        {
            var prompt = BuildAnalyzeDriftPrompt(demo, repo);
            var response = await sessionManager.SendAndWaitAsync(session, prompt);
            var json = ExtractJsonArray(response);

            if (json is null)
            {
                logger.LogInformation("No drift findings JSON found for demo '{Demo}'; returning empty list", demo.Name);
                return [];
            }

            try
            {
                var findings = JsonSerializer.Deserialize<List<DriftFinding>>(json, JsonOptions);
                if (findings is null)
                {
                    logger.LogInformation("Deserialized drift findings were null for demo '{Demo}'; returning empty list", demo.Name);
                    return [];
                }

                logger.LogInformation("Found {FindingCount} drift findings for demo '{Demo}'", findings.Count, demo.Name);
                return findings;
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize drift findings JSON for demo '{Demo}'", demo.Name);
                return [];
            }
        }
        finally
        {
            await sessionManager.DisposeSessionAsync(session);
        }
    }

    private static string BuildIdentifyDemosPrompt(RepoContents repo)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze the following repository and identify discrete demos or concepts being taught.");
        sb.AppendLine($"Repository: {repo.RepoUrl} (branch: {repo.Branch})");
        sb.AppendLine();
        sb.AppendLine("Files in the repository:");
        sb.AppendLine();

        foreach (var file in repo.Files)
        {
            var preview = file.Content.Length > 200
                ? file.Content[..200] + "..."
                : file.Content;
            sb.AppendLine($"--- {file.RelativePath} ---");
            sb.AppendLine(preview);
            sb.AppendLine();
        }

        sb.AppendLine("Return a JSON array of demos. Each demo should have:");
        sb.AppendLine("- Name: short descriptive name");
        sb.AppendLine("- Description: what the demo teaches");
        sb.AppendLine("- Concepts: array of technology concepts demonstrated");
        sb.AppendLine("- FilePaths: array of file paths belonging to this demo");
        sb.AppendLine();
        sb.AppendLine("Return ONLY the JSON array, no additional text.");

        return sb.ToString();
    }

    private static string BuildAnalyzeDriftPrompt(Demo demo, RepoContents repo)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Analyze the demo '{demo.Name}' for drift from current best practices.");
        sb.AppendLine($"Description: {demo.Description}");
        sb.AppendLine($"Concepts: {string.Join(", ", demo.Concepts)}");
        sb.AppendLine();
        sb.AppendLine("Demo files:");
        sb.AppendLine();

        var repoFilesByPath = repo.Files.ToDictionary(f => f.RelativePath, StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in demo.FilePaths)
        {
            if (repoFilesByPath.TryGetValue(filePath, out var file))
            {
                sb.AppendLine($"--- {file.RelativePath} ---");
                sb.AppendLine(file.Content);
                sb.AppendLine();
            }
        }

        sb.AppendLine("Please analyze this demo for drift:");
        sb.AppendLine("1. Check any URLs for validity");
        sb.AppendLine("2. Search the web for current best practices related to the concepts");
        sb.AppendLine("3. Compare the existing code against those best practices");
        sb.AppendLine("4. Identify any drift (outdated patterns, deprecated APIs, broken links, etc.)");
        sb.AppendLine();
        sb.AppendLine("Return a JSON array of drift findings. Each finding should have:");
        sb.AppendLine("- Description: what is outdated or incorrect");
        sb.AppendLine("- Severity: Low, Medium, High, or Critical");
        sb.AppendLine("- AffectedFiles: array of affected file paths");
        sb.AppendLine("- SuggestedFix: how to fix the issue");
        sb.AppendLine("- Category: category of drift (e.g., 'Deprecated API', 'Broken Link', 'Outdated Pattern')");
        sb.AppendLine();
        sb.AppendLine("If no drift is found, return an empty JSON array: []");
        sb.AppendLine("Return ONLY the JSON array, no additional text.");

        return sb.ToString();
    }

    private static Demo CreateFallbackDemo(RepoContents repo) =>
        new(
            Name: "Full Repository",
            Description: "All files in the repository (auto-detected as single demo)",
            Concepts: ["General"],
            FilePaths: repo.Files.Select(f => f.RelativePath).ToList());

    private static string? ExtractJsonArray(string response)
    {
        var startIndex = response.IndexOf('[');
        var endIndex = response.LastIndexOf(']');

        if (startIndex < 0 || endIndex < 0 || endIndex <= startIndex)
        {
            return null;
        }

        return response[startIndex..(endIndex + 1)];
    }
}
