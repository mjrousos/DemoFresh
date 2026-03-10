using DemoFresh.Configuration;

namespace DemoFresh.Tests;

public class Context7ConfigTests
{
    [Fact]
    public void Context7Config_DefaultValues_AreCorrect()
    {
        var config = new Context7Config();

        Assert.True(config.Enabled);
        Assert.Equal("npx", config.Command);
        Assert.Equal(["-y", "@upstash/context7-mcp"], config.Args);
        Assert.Equal(string.Empty, config.ApiKey);
    }

    [Fact]
    public void DemoFreshOptions_Context7_HasDefaultConfig()
    {
        var options = new DemoFreshOptions();

        Assert.NotNull(options.Context7);
        Assert.True(options.Context7.Enabled);
    }

    [Fact]
    public void Context7Config_CustomValues_ArePreserved()
    {
        var config = new Context7Config
        {
            Enabled = false,
            Command = "node",
            Args = ["custom-arg"],
            ApiKey = "test-key-123"
        };

        Assert.False(config.Enabled);
        Assert.Equal("node", config.Command);
        Assert.Equal(["custom-arg"], config.Args);
        Assert.Equal("test-key-123", config.ApiKey);
    }
}
