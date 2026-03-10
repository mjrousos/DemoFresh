using DemoFresh.Configuration;
using DemoFresh.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateDefaultBuilder(args)
    .UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration))
    .ConfigureServices((context, services) =>
    {
        services.AddDemoFresh(context.Configuration);

        var repoUrl = GetCliArg(args, "--repo");
        if (repoUrl is not null)
        {
            services.PostConfigure<DemoFreshOptions>(options =>
            {
                options.Repos.Clear();
                options.Repos.Add(new RepoConfig { Url = repoUrl });
            });
        }
    });

await builder.Build().RunAsync();

static string? GetCliArg(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
