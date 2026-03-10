using DemoFresh.Configuration;
using DemoFresh.Extensions;
using DemoFresh.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MimeKit;
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

// --test-email: send a test email directly via SmtpSender and exit
if (args.Contains("--test-email"))
{
    using var host = builder.Build();
    var options = host.Services.GetRequiredService<IOptions<DemoFreshOptions>>().Value;
    var smtpSender = host.Services.GetRequiredService<ISmtpSender>();
    var logger = host.Services.GetRequiredService<Serilog.ILogger>();
    var recipient = GetCliArg(args, "--test-email-to") ?? options.ReportRecipient;

    logger.Information("Sending test email to {Recipient} via {Host}:{Port}",
        recipient, options.Email.SmtpHost, options.Email.SmtpPort);

    var message = new MimeMessage();
    message.From.Add(new MailboxAddress(options.Email.SenderName, options.Email.SenderAddress));
    message.To.Add(MailboxAddress.Parse(recipient));
    message.Subject = "DemoFresh Test Email";
    message.Body = new TextPart("html")
    {
        Text = "<h1>DemoFresh Test</h1><p>If you see this, email sending is working correctly.</p>"
    };

    try
    {
        await smtpSender.SendAsync(message,
            options.Email.SmtpHost, options.Email.SmtpPort, options.Email.UseSsl,
            options.Email.SenderAddress, options.Email.ClientId, options.Email.ClientSecret);
        logger.Information("Test email sent successfully to {Recipient}", recipient);
    }
    catch (Exception ex)
    {
        logger.Error(ex, "Failed to send test email to {Recipient}", recipient);
    }

    return;
}

await builder.Build().RunAsync();

static string? GetCliArg(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
