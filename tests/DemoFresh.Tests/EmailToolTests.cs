using DemoFresh.Configuration;
using DemoFresh.Services;
using DemoFresh.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using Moq;

namespace DemoFresh.Tests;

public class EmailToolTests
{
    private readonly EmailConfig _config = new()
    {
        SmtpHost = "smtp.test.com",
        SmtpPort = 587,
        UseSsl = true,
        SenderAddress = "sender@test.com",
        SenderName = "Test Sender",
        ClientId = "test-client-id",
        ClientSecret = "test-client-secret"
    };

    private readonly Mock<ISmtpSender> _smtpMock = new();

    private Microsoft.Extensions.AI.AIFunction CreateTool() =>
        EmailToolFactory.Create(_config, _smtpMock.Object, NullLogger.Instance);

    private async Task<object?> InvokeToolAsync(
        Microsoft.Extensions.AI.AIFunction tool,
        string recipient = "test@example.com",
        string subject = "Test Subject",
        string htmlBody = "<h1>Test</h1>")
    {
        return await tool.InvokeAsync(new Microsoft.Extensions.AI.AIFunctionArguments(new Dictionary<string, object?>
        {
            ["recipient"] = recipient,
            ["subject"] = subject,
            ["htmlBody"] = htmlBody
        }));
    }

    [Fact]
    public void Create_ReturnsAIFunction_WithCorrectName()
    {
        var tool = CreateTool();

        Assert.Equal("send_email", tool.Name);
    }

    [Fact]
    public void Create_ReturnsAIFunction_WithDescription()
    {
        var tool = CreateTool();

        Assert.False(string.IsNullOrEmpty(tool.Description));
    }

    [Fact]
    public async Task InvokeAsync_Success_CallsSmtpSender()
    {
        _smtpMock
            .Setup(s => s.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tool = CreateTool();
        await InvokeToolAsync(tool);

        _smtpMock.Verify(
            s => s.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Success_PassesCorrectSmtpConfig()
    {
        string? capturedHost = null;
        int capturedPort = 0;
        bool capturedUseSsl = false;
        string? capturedUsername = null;
        string? capturedClientId = null;
        string? capturedClientSecret = null;

        _smtpMock
            .Setup(s => s.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<MimeMessage, string, int, bool, string, string, string, CancellationToken>(
                (msg, host, port, ssl, user, clientId, clientSecret, ct) =>
                {
                    capturedHost = host;
                    capturedPort = port;
                    capturedUseSsl = ssl;
                    capturedUsername = user;
                    capturedClientId = clientId;
                    capturedClientSecret = clientSecret;
                })
            .Returns(Task.CompletedTask);

        var tool = CreateTool();
        await InvokeToolAsync(tool);

        Assert.Equal(_config.SmtpHost, capturedHost);
        Assert.Equal(_config.SmtpPort, capturedPort);
        Assert.Equal(_config.UseSsl, capturedUseSsl);
        Assert.Equal(_config.SenderName, capturedUsername);
        Assert.Equal(_config.ClientId, capturedClientId);
        Assert.Equal(_config.ClientSecret, capturedClientSecret);
    }

    [Fact]
    public async Task InvokeAsync_Success_BuildsCorrectMessage()
    {
        MimeMessage? capturedMessage = null;

        _smtpMock
            .Setup(s => s.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<MimeMessage, string, int, bool, string, string, string, CancellationToken>(
                (msg, host, port, ssl, user, clientId, clientSecret, ct) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var tool = CreateTool();
        await InvokeToolAsync(tool, "recipient@test.com", "My Subject", "<p>Hello</p>");

        Assert.NotNull(capturedMessage);
        var fromMailbox = Assert.Single(capturedMessage!.From.Mailboxes);
        Assert.Equal(_config.SenderAddress, fromMailbox.Address);
        var toMailbox = Assert.Single(capturedMessage.To.Mailboxes);
        Assert.Equal("recipient@test.com", toMailbox.Address);
        Assert.Equal("My Subject", capturedMessage.Subject);
        Assert.Contains("<p>Hello</p>", capturedMessage.HtmlBody);
    }

    [Fact]
    public async Task InvokeAsync_Success_ReturnsSuccessResult()
    {
        _smtpMock
            .Setup(s => s.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tool = CreateTool();
        var result = await InvokeToolAsync(tool);

        var resultStr = result?.ToString() ?? string.Empty;
        Assert.Contains("success", resultStr);
        Assert.Contains("true", resultStr);
    }

    [Fact]
    public async Task InvokeAsync_SmtpFailure_ReturnsFailureResult()
    {
        _smtpMock
            .Setup(s => s.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP connection failed"));

        var tool = CreateTool();
        var result = await InvokeToolAsync(tool);

        var resultStr = result?.ToString() ?? string.Empty;
        Assert.Contains("false", resultStr);
        Assert.Contains("SMTP connection failed", resultStr);
    }

    [Fact]
    public async Task InvokeAsync_SmtpFailure_DoesNotThrow()
    {
        _smtpMock
            .Setup(s => s.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP connection failed"));

        var tool = CreateTool();

        var exception = await Record.ExceptionAsync(() => InvokeToolAsync(tool));
        Assert.Null(exception);
    }
}
