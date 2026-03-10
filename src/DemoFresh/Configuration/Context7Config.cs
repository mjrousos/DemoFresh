namespace DemoFresh.Configuration;

public record Context7Config
{
    public bool Enabled { get; init; } = true;
    public string Command { get; init; } = "npx";
    public string[] Args { get; init; } = ["-y", "@upstash/context7-mcp"];
    public string ApiKey { get; init; } = string.Empty;
}
