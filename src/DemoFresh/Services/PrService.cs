using DemoFresh.Models;
using Microsoft.Extensions.Logging;

namespace DemoFresh.Services;

public class PrService(IProcessRunner processRunner, ILogger<PrService> logger) : IPrService
{
    public async Task<ActionResult> CreatePullRequestAsync(Demo demo, string plan, string workingDirectory, CancellationToken ct = default)
    {
        var branch = $"demofresh/{demo.Name.ToLowerInvariant().Replace(" ", "-")}-{DateTime.UtcNow:yyyyMMdd}";

        try
        {
            logger.LogInformation("Creating PR for demo '{DemoName}' on branch {Branch}", demo.Name, branch);

            await RunCommandAsync("git", $"checkout -b {branch}", workingDirectory, ct);
            await RunCommandAsync("git", "add -A", workingDirectory, ct);
            await RunCommandAsync("git", $"commit -m \"DemoFresh: Update {demo.Name} for current best practices\"", workingDirectory, ct);
            await RunCommandAsync("git", $"push -u origin {branch}", workingDirectory, ct);

            var prOutput = await RunCommandAsync(
                "gh",
                $"pr create --title \"DemoFresh: Update {demo.Name}\" --body-file - --head {branch}",
                workingDirectory,
                ct,
                standardInput: plan);

            var prUrl = ParsePrUrl(prOutput);

            logger.LogInformation("PR created successfully: {PrUrl}", prUrl);
            return new ActionResult(ActionResultType.PrCreated, prUrl, DelegationConfirmation: null, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create PR for demo '{DemoName}'. Exception: {ExceptionMessage}", demo.Name, ex.Message);
            return new ActionResult(ActionResultType.Failed, PrUrl: null, DelegationConfirmation: null, ErrorMessage: ex.Message);
        }
    }

    private async Task<string> RunCommandAsync(string command, string args, string workingDir, CancellationToken ct, string? standardInput = null)
    {
        logger.LogDebug("Running: {Command} {Args} in {WorkingDir}", command, args, workingDir);

        var result = await processRunner.RunAsync(command, args, workingDir, ct, standardInput);

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            logger.LogDebug("{Command} stdout: {Output}", command, result.StandardOutput.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            logger.LogDebug("{Command} stderr: {Error}", command, result.StandardError.TrimEnd());
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{command} {args.Split(' ')[0]} failed with exit code {result.ExitCode}: {result.StandardError}");
        }

        return result.StandardOutput.Trim();
    }

    private static string? ParsePrUrl(string output)
    {
        // gh pr create outputs the PR URL as the last line
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return line;
            }
        }

        return output.Trim();
    }
}
