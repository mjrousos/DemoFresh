using System.Text;
using System.Web;
using DemoFresh.Models;
using Microsoft.Extensions.Logging;

namespace DemoFresh.Services;

public class ReportGenerator(ILogger<ReportGenerator> logger) : IReportGenerator
{
    public string GenerateHtmlReport(AnalysisReport report)
    {
        logger.LogDebug("Generating HTML report for {RepoUrl} ({Branch})", report.RepoUrl, report.Branch);

        var allFindings = report.Demos.SelectMany(d => d.Findings).ToList();
        var criticalCount = allFindings.Count(f => f.Severity == DriftSeverity.Critical);
        var highCount = allFindings.Count(f => f.Severity == DriftSeverity.High);
        var mediumCount = allFindings.Count(f => f.Severity == DriftSeverity.Medium);
        var lowCount = allFindings.Count(f => f.Severity == DriftSeverity.Low);

        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset=\"utf-8\"></head>");
        sb.AppendLine("<body style=\"font-family:Segoe UI,Arial,sans-serif;margin:0;padding:0;background-color:#f4f4f4;\">");

        // Header
        sb.AppendLine("<div style=\"background-color:#24292e;color:#ffffff;padding:20px 30px;\">");
        sb.AppendLine("<h1 style=\"margin:0;font-size:24px;\">DemoFresh Analysis Report</h1>");
        sb.AppendLine("</div>");

        // Repo info
        sb.AppendLine("<div style=\"padding:20px 30px;background-color:#ffffff;\">");
        sb.AppendLine($"<p><strong>Repository:</strong> {Encode(report.RepoUrl)}</p>");
        sb.AppendLine($"<p><strong>Branch:</strong> {Encode(report.Branch)}</p>");
        sb.AppendLine($"<p><strong>Analyzed:</strong> {report.AnalyzedAt:yyyy-MM-dd HH:mm:ss zzz}</p>");

        // Summary
        sb.AppendLine("<h2 style=\"border-bottom:1px solid #e1e4e8;padding-bottom:8px;\">Summary</h2>");
        sb.AppendLine($"<p><strong>Total Demos:</strong> {report.Demos.Count}</p>");
        sb.AppendLine($"<p><strong>Total Findings:</strong> {allFindings.Count}</p>");
        sb.AppendLine("<table style=\"border-collapse:collapse;margin-bottom:20px;\">");
        sb.AppendLine("<tr>");
        AppendSeverityBadge(sb, "Critical", criticalCount, "#d73a49");
        AppendSeverityBadge(sb, "High", highCount, "#e36209");
        AppendSeverityBadge(sb, "Medium", mediumCount, "#dbab09");
        AppendSeverityBadge(sb, "Low", lowCount, "#28a745");
        sb.AppendLine("</tr></table>");

        // Per-demo sections
        foreach (var analysis in report.Demos)
        {
            sb.AppendLine("<div style=\"margin-top:24px;border:1px solid #e1e4e8;border-radius:6px;overflow:hidden;\">");

            // Demo header
            sb.AppendLine($"<div style=\"background-color:#f6f8fa;padding:12px 16px;border-bottom:1px solid #e1e4e8;\">");
            sb.AppendLine($"<h3 style=\"margin:0;\">{Encode(analysis.Demo.Name)}</h3>");
            sb.AppendLine($"<p style=\"margin:4px 0 0;color:#586069;\">{Encode(analysis.Demo.Description)}</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div style=\"padding:16px;\">");

            // Concepts
            if (analysis.Demo.Concepts.Count > 0)
            {
                sb.AppendLine("<p><strong>Concepts:</strong> ");
                sb.AppendLine(string.Join(", ", analysis.Demo.Concepts.Select(c =>
                    $"<span style=\"display:inline-block;background-color:#e1e4e8;border-radius:12px;padding:2px 10px;margin:2px;font-size:12px;\">{Encode(c)}</span>")));
                sb.AppendLine("</p>");
            }

            // Findings table
            if (analysis.Findings.Count > 0)
            {
                sb.AppendLine("<table style=\"width:100%;border-collapse:collapse;margin-top:12px;\">");
                sb.AppendLine("<tr style=\"background-color:#f6f8fa;text-align:left;\">");
                sb.AppendLine("<th style=\"padding:8px;border:1px solid #e1e4e8;\">Description</th>");
                sb.AppendLine("<th style=\"padding:8px;border:1px solid #e1e4e8;\">Severity</th>");
                sb.AppendLine("<th style=\"padding:8px;border:1px solid #e1e4e8;\">Category</th>");
                sb.AppendLine("<th style=\"padding:8px;border:1px solid #e1e4e8;\">Affected Files</th>");
                sb.AppendLine("</tr>");

                foreach (var finding in analysis.Findings)
                {
                    var severityColor = GetSeverityColor(finding.Severity);
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td style=\"padding:8px;border:1px solid #e1e4e8;\">{Encode(finding.Description)}</td>");
                    sb.AppendLine($"<td style=\"padding:8px;border:1px solid #e1e4e8;\"><span style=\"background-color:{severityColor};color:#fff;padding:2px 8px;border-radius:4px;font-size:12px;\">{finding.Severity}</span></td>");
                    sb.AppendLine($"<td style=\"padding:8px;border:1px solid #e1e4e8;\">{Encode(finding.Category)}</td>");
                    sb.AppendLine($"<td style=\"padding:8px;border:1px solid #e1e4e8;font-size:12px;\">{Encode(string.Join(", ", finding.AffectedFiles))}</td>");
                    sb.AppendLine("</tr>");
                }

                sb.AppendLine("</table>");
            }
            else
            {
                sb.AppendLine("<p style=\"color:#28a745;\">&#10003; No findings</p>");
            }

            // Plan
            if (analysis.Plan is not null)
            {
                sb.AppendLine("<div style=\"margin-top:12px;background-color:#f1f8ff;border:1px solid #c8e1ff;border-radius:4px;padding:12px;\">");
                sb.AppendLine($"<strong>Plan:</strong> {Encode(analysis.Plan)}");
                sb.AppendLine("</div>");
            }

            // Action result
            if (analysis.Action is not null)
            {
                sb.AppendLine("<div style=\"margin-top:12px;\">");
                sb.Append("<strong>Action:</strong> ");
                switch (analysis.Action.Type)
                {
                    case ActionResultType.PrCreated:
                        sb.AppendLine($"<a href=\"{Encode(analysis.Action.PrUrl ?? "")}\" style=\"color:#0366d6;\">PR Created</a>");
                        break;
                    case ActionResultType.Delegated:
                        sb.AppendLine($"<span style=\"color:#6f42c1;\">Delegated</span> &mdash; {Encode(analysis.Action.DelegationConfirmation ?? "")}");
                        break;
                    case ActionResultType.NoActionNeeded:
                        sb.AppendLine("<span style=\"color:#28a745;\">No action needed</span>");
                        break;
                    case ActionResultType.Failed:
                        sb.AppendLine($"<span style=\"color:#d73a49;\">Failed</span> &mdash; {Encode(analysis.Action.ErrorMessage ?? "")}");
                        break;
                }
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</div></div>"); // close padding div and demo section
        }

        sb.AppendLine("</div>"); // close main content

        // Footer
        sb.AppendLine("<div style=\"background-color:#24292e;color:#959da5;padding:16px 30px;text-align:center;font-size:12px;\">");
        sb.AppendLine("<p style=\"margin:0;\">Generated by <strong style=\"color:#ffffff;\">DemoFresh</strong> &bull; Keeping demos fresh and up to date</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("</body></html>");

        logger.LogInformation("HTML report generated ({Length} chars, {DemoCount} demos, {FindingCount} findings)",
            sb.Length, report.Demos.Count, allFindings.Count);

        return sb.ToString();
    }

    public string GenerateConsoleSummary(AnalysisReport report)
    {
        logger.LogDebug("Generating console summary for {RepoUrl} ({Branch})", report.RepoUrl, report.Branch);

        var sb = new StringBuilder();
        sb.AppendLine($"Repository: {report.RepoUrl} ({report.Branch})");
        sb.AppendLine(new string('-', 60));

        var totalFindings = 0;

        foreach (var analysis in report.Demos)
        {
            var findingCount = analysis.Findings.Count;
            totalFindings += findingCount;

            var actionText = analysis.Action?.Type switch
            {
                ActionResultType.PrCreated => "PR created",
                ActionResultType.Delegated => "Delegated",
                ActionResultType.NoActionNeeded => "No action needed",
                ActionResultType.Failed => "Failed",
                null => "Pending",
                _ => analysis.Action.Type.ToString()
            };

            sb.AppendLine($"  {analysis.Demo.Name}: {findingCount} finding(s) -> {actionText}");
        }

        sb.AppendLine(new string('-', 60));
        sb.AppendLine($"Total: {report.Demos.Count} demo(s), {totalFindings} finding(s)");

        return sb.ToString();
    }

    private static void AppendSeverityBadge(StringBuilder sb, string label, int count, string color)
    {
        sb.AppendLine($"<td style=\"padding:4px 12px;text-align:center;\"><span style=\"background-color:{color};color:#fff;padding:4px 12px;border-radius:4px;font-weight:bold;\">{count}</span><br><span style=\"font-size:12px;color:#586069;\">{label}</span></td>");
    }

    private static string GetSeverityColor(DriftSeverity severity) => severity switch
    {
        DriftSeverity.Critical => "#d73a49",
        DriftSeverity.High => "#e36209",
        DriftSeverity.Medium => "#dbab09",
        DriftSeverity.Low => "#28a745",
        _ => "#586069"
    };

    private static string Encode(string value) => HttpUtility.HtmlEncode(value);
}
