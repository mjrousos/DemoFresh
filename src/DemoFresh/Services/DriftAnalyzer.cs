using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DemoFresh.Configuration;
using DemoFresh.Models;
using Microsoft.Extensions.Logging;

namespace DemoFresh.Services;

public sealed class DriftAnalyzer(
    ICopilotSessionManager sessionManager,
    ILogger<DriftAnalyzer> logger) : IDriftAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyList<Demo>> IdentifyDemosAsync(
        RepoContents repo, string model, Context7Config? context7 = null, CancellationToken ct = default)
    {
        var session = await sessionManager.CreateAnalysisSessionAsync(model, context7);
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
        Demo demo, RepoContents repo, string model, Context7Config? context7 = null, CancellationToken ct = default)
    {
        var session = await sessionManager.CreateAnalysisSessionAsync(model, context7);
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

        sb.AppendLine("""
            Respond with ONLY a JSON array (no markdown, no explanation, no code fences).

            JSON Schema:
            [
              {
                "Name": "string (short descriptive name for the demo)",
                "Description": "string (what the demo teaches)",
                "Concepts": ["string (technology concepts demonstrated)"],
                "FilePaths": ["string (relative file paths belonging to this demo)"]
              }
            ]

            Example output:
            [
              {
                "Name": "JWT Authentication",
                "Description": "Demonstrates how to implement JWT-based authentication in ASP.NET Core",
                "Concepts": ["ASP.NET Core", "JWT", "Authentication", "Middleware"],
                "FilePaths": ["src/Auth/JwtHandler.cs", "src/Auth/AuthMiddleware.cs", "src/Auth/README.md"]
              },
              {
                "Name": "Entity Framework Basics",
                "Description": "Shows basic CRUD operations with Entity Framework Core",
                "Concepts": ["Entity Framework Core", "CRUD", "SQL Server"],
                "FilePaths": ["src/Data/AppDbContext.cs", "src/Data/UserRepository.cs"]
              }
            ]

            IMPORTANT: Return ONLY the raw JSON array. Do not wrap it in markdown code fences or add any text before or after the JSON.
            """);

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
        sb.AppendLine("1. Use Context7 to look up current documentation for the libraries and frameworks used in the demo");
        sb.AppendLine("2. Check any URLs for validity");
        sb.AppendLine("3. Search the web for current best practices related to the concepts");
        sb.AppendLine("4. Compare the demo's usage patterns against the latest official documentation");
        sb.AppendLine("5. Identify any drift (outdated patterns, deprecated APIs, broken links, etc.)");
        sb.AppendLine();
        sb.AppendLine("""
            Respond with ONLY a JSON array (no markdown, no explanation, no code fences).

            JSON Schema:
            [
              {
                "Description": "string (what is outdated or incorrect)",
                "Severity": "Low | Medium | High | Critical",
                "AffectedFiles": ["string (relative file paths affected)"],
                "SuggestedFix": "string (how to fix the issue)",
                "Category": "string (e.g., 'Deprecated API', 'Broken Link', 'Outdated Pattern', 'Version Drift', 'Missing Best Practice')"
              }
            ]

            Example output:
            [
              {
                "Description": "Using AddJsonFile() without reloadOnChange in .NET 10; current best practice is to use the WebApplicationBuilder which handles this automatically",
                "Severity": "Medium",
                "AffectedFiles": ["src/Program.cs"],
                "SuggestedFix": "Replace Host.CreateDefaultBuilder with WebApplication.CreateBuilder which auto-configures JSON config providers",
                "Category": "Outdated Pattern"
              },
              {
                "Description": "Package Newtonsoft.Json 12.0.3 is outdated; current version is 13.0.3 with security fixes",
                "Severity": "High",
                "AffectedFiles": ["src/MyApp.csproj"],
                "SuggestedFix": "Update Newtonsoft.Json to 13.0.3, or migrate to System.Text.Json which is the recommended JSON library for .NET",
                "Category": "Version Drift"
              }
            ]

            If no drift is found, return an empty JSON array: []

            IMPORTANT: Return ONLY the raw JSON array. Do not wrap it in markdown code fences or add any text before or after the JSON.
            """);

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
