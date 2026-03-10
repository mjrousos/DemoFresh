using MimeKit;

namespace DemoFresh.Services;

public interface ISmtpSender
{
    Task SendAsync(MimeMessage message, string host, int port, bool useSsl, string username, string clientId, string clientSecret, CancellationToken ct = default);
}
