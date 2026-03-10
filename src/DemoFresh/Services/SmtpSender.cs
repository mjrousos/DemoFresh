using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace DemoFresh.Services;

public class SmtpSender : ISmtpSender
{
    public async Task SendAsync(MimeMessage message, string host, int port, bool useSsl, string username, string password, CancellationToken ct = default)
    {
        using var client = new SmtpClient();
        var options = useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(host, port, options, ct);
        await client.AuthenticateAsync(username, password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
