using DemoFresh.Configuration;
using DemoFresh.Models;
using DemoFresh.Tools;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DemoFresh.Services;

public class AnalysisOrchestrator : BackgroundService
{
    private readonly IRepoService _repoService;
    private readonly ICopilotSessionManager _sessionManager;
    private readonly IDriftAnalyzer _driftAnalyzer;
    private readonly IPlanExecutor _planExecutor;
    private readonly IPrService _prService;
    private readonly IDelegationService _delegationService;
    private readonly IReportGenerator _reportGenerator;
    private readonly ISmtpSender _smtpSender;
    private readonly DemoFreshOptions _options;
    private readonly ILogger<AnalysisOrchestrator> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public AnalysisOrchestrator(
        IRepoService repoService,
        ICopilotSessionManager sessionManager,
        IDriftAnalyzer driftAnalyzer,
        IPlanExecutor planExecutor,
        IPrService prService,
        IDelegationService delegationService,
        IReportGenerator reportGenerator,
        ISmtpSender smtpSender,
        IOptions<DemoFreshOptions> options,
        ILogger<AnalysisOrchestrator> logger,
        IHostApplicationLifetime lifetime)
    {
        _repoService = repoService;
        _sessionManager = sessionManager;
        _driftAnalyzer = driftAnalyzer;
        _planExecutor = planExecutor;
        _prService = prService;
        _delegationService = delegationService;
        _reportGenerator = reportGenerator;
        _smtpSender = smtpSender;
        _options = options.Value;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting analysis orchestrator with {RepoCount} repos configured", _options.Repos.Count);
            await _sessionManager.InitializeAsync();

            foreach (var repo in _options.Repos)
            {
                try
                {
                    await ProcessRepoAsync(repo, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing repo {RepoUrl}", repo.Url);
                }
            }
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private async Task ProcessRepoAsync(RepoConfig repo, CancellationToken ct)
    {
        var repoContents = await _repoService.CloneAndEnumerateAsync(repo.Url, repo.Branch, ct);

        try
        {
            var demos = await _driftAnalyzer.IdentifyDemosAsync(repoContents, _options.Model, _options.Context7, ct);
            var demoAnalyses = new List<DemoAnalysis>();

            foreach (var demo in demos)
            {
                var analysis = await AnalyzeDemoAsync(demo, repoContents, ct);
                demoAnalyses.Add(analysis);
            }

            var report = new AnalysisReport(
                repo.Url,
                repo.Branch ?? "main",
                DateTimeOffset.UtcNow,
                demoAnalyses);

            var consoleSummary = _reportGenerator.GenerateConsoleSummary(report);
            Console.WriteLine(consoleSummary);

            await SendEmailReportAsync(report, ct);
        }
        finally
        {
            await _repoService.CleanupAsync(repoContents.WorkingDirectory);
        }
    }

    private async Task<DemoAnalysis> AnalyzeDemoAsync(Demo demo, RepoContents repoContents, CancellationToken ct)
    {
        var findings = await _driftAnalyzer.AnalyzeDriftAsync(demo, repoContents, _options.Model, _options.Context7, ct);

        if (findings.Count == 0)
        {
            return new DemoAnalysis(
                demo,
                findings,
                Plan: null,
                Action: new ActionResult(ActionResultType.NoActionNeeded, null, null, null));
        }

        var plan = await _planExecutor.GeneratePlanAsync(demo, findings, _options.Model, ct);

        var actionResult = _options.ActionMode switch
        {
            ActionMode.CreatePR => await CreatePrAsync(demo, plan, repoContents.WorkingDirectory, ct),
            ActionMode.DelegateToCodingAgent => await _delegationService.DelegateToAgentAsync(demo, plan, _options.Model, ct),
            _ => new ActionResult(ActionResultType.Failed, null, null, $"Unknown action mode: {_options.ActionMode}")
        };

        return new DemoAnalysis(demo, findings, plan, actionResult);
    }

    private async Task<ActionResult> CreatePrAsync(Demo demo, string plan, string workingDirectory, CancellationToken ct)
    {
        await _planExecutor.ExecutePlanAsync(plan, workingDirectory, _options.Model, ct);
        return await _prService.CreatePullRequestAsync(demo, plan, workingDirectory, ct);
    }

    private bool IsEmailConfigured =>
        !string.IsNullOrEmpty(_options.Email.SenderAddress) &&
        !string.IsNullOrEmpty(_options.Email.ClientId) &&
        !string.IsNullOrEmpty(_options.Email.ClientSecret) &&
        !string.IsNullOrEmpty(_options.ReportRecipient);

    private async Task SendEmailReportAsync(AnalysisReport report, CancellationToken ct)
    {
        if (!IsEmailConfigured)
        {
            _logger.LogWarning("Email is not configured (missing SenderAddress, ClientId, ClientSecret, or ReportRecipient); skipping email report");
            return;
        }

        var htmlReport = _reportGenerator.GenerateHtmlReport(report);
        var emailTool = EmailToolFactory.Create(_options.Email, _smtpSender, _logger);

        var session = await _sessionManager.CreateAnalysisSessionAsync(_options.Model, tools: [emailTool]);
        var prompt = $"Send an email to {_options.ReportRecipient} with the subject " +
                     $"\"DemoFresh Report: {report.RepoUrl}\" and the following HTML body:\n\n{htmlReport}";

        await _sessionManager.SendAndWaitAsync(session, prompt);
    }
}
