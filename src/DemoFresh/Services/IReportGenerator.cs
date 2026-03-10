using DemoFresh.Models;

namespace DemoFresh.Services;

public interface IReportGenerator
{
    string GenerateHtmlReport(AnalysisReport report);
    string GenerateConsoleSummary(AnalysisReport report);
}
