namespace DemoFresh.Configuration;

public record DemoFreshOptions
{
    public List<RepoConfig> Repos { get; init; } = [];
    public ActionMode ActionMode { get; init; } = ActionMode.CreatePR;
    public string Model { get; init; } = "gpt-5";
    public string ReportRecipient { get; init; } = string.Empty;
    public EmailConfig Email { get; init; } = new();
}
