using JobRadar.Agents.Spike;

namespace JobRadar.Agents.Tests;

public class SpikeTests
{
    [Fact]
    public async Task Fake_mode_runs_without_a_token_and_prints_every_stage()
    {
        var writer = new StringWriter();
        var code = await CopilotSpike.RunAsync(["--fake"], writer, CancellationToken.None);
        var text = writer.ToString();
        Assert.Equal(0, code);
        Assert.Contains("client mode Empty", text);
        Assert.Contains("tool call save_probe", text);
        Assert.Contains("shell attempt refused", text);
    }

    [Fact]
    public async Task Live_mode_without_a_token_calls_nothing_and_fails_loudly()
    {
        var previous = Environment.GetEnvironmentVariable("COPILOT_GITHUB_TOKEN");
        Environment.SetEnvironmentVariable("COPILOT_GITHUB_TOKEN", null);
        try
        {
            var writer = new StringWriter();
            var code = await CopilotSpike.RunAsync([], writer, CancellationToken.None);
            Assert.Equal(1, code);
            Assert.Contains("nothing was called", writer.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("COPILOT_GITHUB_TOKEN", previous);
        }
    }
}
