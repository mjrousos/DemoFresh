using System.Diagnostics;

namespace DemoFresh.Services;

public class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string command, string arguments, string? workingDirectory = null, CancellationToken ct = default, string? standardInput = null)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? string.Empty,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = standardInput is not null,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput);
            process.StandardInput.Close();
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new ProcessResult(process.ExitCode, stdout, stderr);
    }
}
