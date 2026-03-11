using System.Text;
using DemoFresh.Models;
using Microsoft.Extensions.Logging;

namespace DemoFresh.Services;

public class PlanExecutor(ICopilotSessionManager sessionManager, ILogger<PlanExecutor> logger) : IPlanExecutor
{
    public async Task<string> GeneratePlanAsync(Demo demo, IReadOnlyList<DriftFinding> findings, CancellationToken ct = default)
    {
        var session = await sessionManager.CreatePlanningSessionAsync();
        await using var _ = session;

        var prompt = BuildPlanningPrompt(demo, findings);
        logger.LogInformation("Generating plan for demo '{DemoName}' with {FindingCount} findings", demo.Name, findings.Count);

        var plan = await sessionManager.SendAndWaitAsync(session, prompt);
        logger.LogDebug("Plan generated for demo '{DemoName}'", demo.Name);

        return plan;
    }

    public async Task ExecutePlanAsync(string plan, string workingDirectory, CancellationToken ct = default)
    {
        var session = await sessionManager.CreateExecutionSessionAsync();
        await using var _ = session;

        var prompt = $"""
            Working directory: {workingDirectory}

            Execute the following plan:

            {plan}
            """;

        logger.LogInformation("Executing plan in '{WorkingDirectory}'", workingDirectory);
        var response = await sessionManager.SendAndWaitAsync(session, prompt);
        logger.LogInformation("Plan execution completed. Response: {Response}", response);
    }

    private static string BuildPlanningPrompt(Demo demo, IReadOnlyList<DriftFinding> findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Demo: {demo.Name}");
        sb.AppendLine();
        sb.AppendLine($"**Description:** {demo.Description}");
        sb.AppendLine();

        sb.AppendLine("**Concepts:**");
        foreach (var concept in demo.Concepts)
        {
            sb.AppendLine($"- {concept}");
        }

        sb.AppendLine();
        sb.AppendLine("**Files:**");
        foreach (var file in demo.FilePaths)
        {
            sb.AppendLine($"- {file}");
        }

        sb.AppendLine();
        sb.AppendLine($"## Drift Findings ({findings.Count})");
        sb.AppendLine();

        for (var i = 0; i < findings.Count; i++)
        {
            var finding = findings[i];
            sb.AppendLine($"### Finding {i + 1}: [{finding.Severity}] {finding.Category}");
            sb.AppendLine();
            sb.AppendLine($"**Description:** {finding.Description}");
            sb.AppendLine($"**Severity:** {finding.Severity}");
            sb.AppendLine($"**Affected files:**");
            foreach (var file in finding.AffectedFiles)
            {
                sb.AppendLine($"- {file}");
            }

            sb.AppendLine($"**Suggested fix:** {finding.SuggestedFix}");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Based on the demo details and drift findings above, produce a detailed implementation plan to resolve all findings. ");
        sb.AppendLine("Prioritize by severity (Critical > High > Medium > Low). ");
        sb.AppendLine("For each step, specify the file(s) to modify and the exact changes needed.");

        return sb.ToString();
    }
}
