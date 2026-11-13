namespace JobRadar.Cli.Commands;

/// <summary>One command of the jobradar tool. Registered by convention: every public non-abstract implementation is found by AddCommands.</summary>
public interface ICliCommand
{
    string Name { get; }

    Task<int> RunAsync(string[] args, CancellationToken ct);
}
