using DemoFresh.Configuration;

namespace DemoFresh.Tests;

public class ConfigurationTests
{
    [Fact]
    public void DemoFreshOptions_DefaultValues_AreCorrect()
    {
        var options = new DemoFreshOptions();

        Assert.Equal(ActionMode.CreatePR, options.ActionMode);
        Assert.Equal("gpt-5", options.Model);
    }

    [Fact]
    public void RepoConfig_DefaultBranch_IsMain()
    {
        var repo = new RepoConfig();

        Assert.Equal("main", repo.Branch);
    }

    [Fact]
    public void EmailConfig_DefaultPort_Is587()
    {
        var email = new EmailConfig();

        Assert.Equal(587, email.SmtpPort);
    }

    [Fact]
    public void ActionMode_HasExpectedValues()
    {
        Assert.Contains(ActionMode.CreatePR, Enum.GetValues<ActionMode>());
        Assert.Contains(ActionMode.DelegateToCodingAgent, Enum.GetValues<ActionMode>());
    }
}
