using JobRadar.Agents.Spike;

namespace JobRadar.Cli.Commands;

/// <summary>`jobradar spike [--fake] [--model id]`: the owner's local Copilot check (D-20 amended, T-00.05).</summary>
public sealed class SpikeCommand : ICliCommand
{
    public string Name => "spike";

    public Task<int> RunAsync(string[] args, CancellationToken ct) => CopilotSpike.RunAsync(args, Console.Out, ct);
}
