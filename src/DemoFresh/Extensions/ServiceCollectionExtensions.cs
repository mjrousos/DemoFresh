using DemoFresh.Configuration;
using DemoFresh.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DemoFresh.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDemoFresh(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<DemoFreshOptions>(config.GetSection("DemoFresh"));

        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<ICopilotSessionManager, CopilotSessionManager>();
        services.AddTransient<IRepoService, RepoService>();
        services.AddTransient<IDriftAnalyzer, DriftAnalyzer>();
        services.AddTransient<IPlanExecutor, PlanExecutor>();
        services.AddTransient<IPrService, PrService>();
        services.AddTransient<IDelegationService, DelegationService>();
        services.AddTransient<IReportGenerator, ReportGenerator>();
        services.AddSingleton<ISmtpSender, SmtpSender>();
        services.AddHostedService<AnalysisOrchestrator>();

        return services;
    }
}
