using System.ComponentModel;
using DemoFresh.Configuration;
using DemoFresh.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace DemoFresh.Tools;

public static class EmailToolFactory
{
    public static AIFunction Create(EmailConfig config, ISmtpSender smtpSender, ILogger logger)
    {
        return AIFunctionFactory.Create(
            async ([Description("Recipient email address")] string recipient,
                   [Description("Email subject line")] string subject,
                   [Description("HTML email body")] string htmlBody) =>
            {
                try
                {
                    var message = new MimeMessage();
                    message.From.Add(new MailboxAddress(config.SenderName, config.SenderAddress));
                    message.To.Add(MailboxAddress.Parse(recipient));
                    message.Subject = subject;
                    message.Body = new TextPart("html") { Text = htmlBody };

                    await smtpSender.SendAsync(message, config.SmtpHost, config.SmtpPort, config.UseSsl, config.SenderAddress, config.AppPassword);

                    logger.LogInformation("Email sent to {Recipient} with subject \"{Subject}\"", recipient, subject);
                    return new { Success = true, Message = $"Email sent successfully to {recipient}" };
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send email to {Recipient}", recipient);
                    return new { Success = false, Message = $"Failed to send email: {ex.Message}" };
                }
            },
            "send_email",
            "Send an email report to the specified recipient");
    }
}
