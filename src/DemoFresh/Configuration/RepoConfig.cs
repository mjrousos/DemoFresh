namespace DemoFresh.Configuration;

public record RepoConfig
{
    public string Url { get; init; } = string.Empty;
    public string? Branch { get; init; } = "main";
    public string? Name { get; init; }
}
