using DemoFresh.Configuration;
using DemoFresh.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace DemoFresh.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDemoFresh(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<DemoFreshOptions>(config.GetSection("DemoFresh"));

        services.AddSingleton<IAnsiConsole>(_ => AnsiConsole.Console);
        services.AddSingleton<IConsoleDisplay, ConsoleDisplayService>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<ICopilotSessionManager, CopilotSessionManager>();
        services.AddTransient<IRepoService, RepoService>();
        services.AddTransient<IDriftAnalyzer, DriftAnalyzer>();
        services.AddTransient<IPlanExecutor, PlanExecutor>();
        services.AddTransient<IPrService, PrService>();
        services.AddTransient<IReportGenerator, ReportGenerator>();
        services.AddSingleton<ISmtpSender, SmtpSender>();
        services.AddHostedService<AnalysisOrchestrator>();

        return services;
    }
}
