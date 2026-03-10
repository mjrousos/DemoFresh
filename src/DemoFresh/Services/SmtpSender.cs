using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace DemoFresh.Services;

public class SmtpSender : ISmtpSender
{
    public async Task SendAsync(MimeMessage message, string host, int port, bool useSsl, string username, string clientId, string clientSecret, CancellationToken ct = default)
    {
        var codeFlow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            DataStore = new FileDataStore("CredentialCacheFolder", false),
            Scopes = [ "https://mail.google.com/" ],
            ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
            LoginHint = username
        });

        var codeReceiver = new LocalServerCodeReceiver();
        var authCode = new AuthorizationCodeInstalledApp(codeFlow, codeReceiver);

        var credential = await authCode.AuthorizeAsync(username, ct);

        if (credential.Token.IsStale)
        {
            await credential.RefreshTokenAsync(ct);
        }

        var oauth2 = new SaslMechanismOAuth2(username, credential.Token.AccessToken);

        using var client = new SmtpClient();
        var options = useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(host, port, options, ct);
        await client.AuthenticateAsync(oauth2, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
