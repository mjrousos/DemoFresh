namespace DemoFresh.Configuration;

public record EmailConfig
{
    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string SenderAddress { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string AppPassword { get; init; } = string.Empty;
}
