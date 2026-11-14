using System.Globalization;
using JobRadar.Cli.Stages;
using JobRadar.Core.Ports;

namespace JobRadar.Cli.Commands;

/// <summary>
/// <c>jobradar ingest</c> (SPEC §6.9): normalises, prefilters, de-duplicates and applies lifecycle to the offers of <c>raw/</c>.
/// <c>--profile</c> is accepted and ignored until a later task needs it.
/// </summary>
public sealed class IngestCommand(IEnumerable<IIngestHook> hooks) : ICliCommand
{
    private const string Usage = "usage: jobradar ingest [--data-dir d] [--config-dir d] [--run-date yyyy-MM-dd] [--profile fake|live]";

    public string Name => "ingest";

    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        var dataDir = ".";
        var configDir = "config";
        DateOnly? runDate = null;

        for (var i = 0; i < args.Length; i++)
        {
            var hasValue = i + 1 < args.Length;
            switch (args[i])
            {
                case "--data-dir" when hasValue:
                    dataDir = args[++i];
                    break;

                case "--config-dir" when hasValue:
                    configDir = args[++i];
                    break;

                case "--run-date" when hasValue:
                    if (!DateOnly.TryParseExact(args[++i], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    {
                        return Refuse();
                    }

                    runDate = parsed;
                    break;

                case "--profile" when hasValue:
                    var profile = args[++i];
                    if (profile is not ("fake" or "live"))
                    {
                        return Refuse();
                    }

                    break;

                default:
                    return Refuse();
            }
        }

        // The only place the clock is read (SPEC R10): the run date defaults to today's UTC date.
        var day = runDate ?? DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        var stage = new IngestStage(hooks.ToList(), Console.Out);
        return await stage.RunAsync(new IngestOptions(dataDir, configDir, day), ct);
    }

    private static int Refuse()
    {
        Console.Error.WriteLine(Usage);
        return 2;
    }
}
