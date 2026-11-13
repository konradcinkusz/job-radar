namespace JobRadar.Cli.Commands;

/// <summary>Skeleton of `jobradar doctor` (P8): prints the state of optional integrations. Filled in by later tasks.</summary>
public sealed class DoctorCommand : ICliCommand
{
    public string Name => "doctor";

    public Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        Console.WriteLine("jobradar doctor: scaffold only, no integrations are wired yet");
        return Task.FromResult(0);
    }
}
