using System.Globalization;
using System.Text.Json;
using JobRadar.Config;
using JobRadar.Core.Filtering;
using JobRadar.Core.Identity;
using JobRadar.Core.Models;
using JobRadar.Core.Ports;
using JobRadar.Store;
using JobRadar.Store.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Cli.Stages;

/// <summary>Inputs of one <c>ingest</c> run. <see cref="RunDate"/> is the run date (SPEC R10); the stage never reads a clock.</summary>
public sealed record IngestOptions(string DataDir, string ConfigDir, DateOnly RunDate);

/// <summary>
/// The body of <c>jobradar ingest</c> (SPEC §6.9, §6.6 stage 1, §5 R7, R8): reads <c>raw/&lt;source&gt;.jsonl</c>, prefilters,
/// upserts, recomputes canonical groups, runs the ingest hooks, counts missed runs and archives. Writes <c>ingest-result.json</c>.
/// Running it twice for one run date gives the same rows and the same result file.
/// </summary>
public sealed class IngestStage(IReadOnlyList<IIngestHook> hooks, TextWriter output)
{
    private const int ArchiveAfterDays = 30;
    private const string DateFormat = "yyyy-MM-dd";
    private const string ResultFileName = "ingest-result.json";

    private static readonly JsonSerializerOptions ResultOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>Returns 0 on success, 2 when a config file has errors (nothing else is done then). Unhealthy sources are never an error.</summary>
    public async Task<int> RunAsync(IngestOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);

        // (1) config: every error is printed, and nothing else happens.
        var sourcesResult = ConfigLoader.LoadSources(Path.Combine(options.ConfigDir, "sources.yaml"));
        var criteriaResult = ConfigLoader.LoadCriteria(Path.Combine(options.ConfigDir, "criteria.yaml"));
        var blocklistResult = ConfigLoader.LoadBlocklist(Path.Combine(options.ConfigDir, "blocklist.yaml"));
        var configErrors = sourcesResult.Errors.Concat(criteriaResult.Errors).Concat(blocklistResult.Errors).ToList();
        if (configErrors.Count > 0)
        {
            foreach (var error in configErrors)
            {
                output.WriteLine($"config error: {error.File} {error.Path}: {error.Rule}");
            }

            return 2;
        }

        var configs = sourcesResult.Value!.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
        var criteria = criteriaResult.Value!;
        var blocklist = blocklistResult.Value!;

        // (2) schema
        Directory.CreateDirectory(options.DataDir);
        var dbPath = Path.Combine(options.DataDir, "jobradar.db");
        await Migrate.RunAsync(dbPath, ct);

        // (3) health from the scrape summary; a missing source or file counts as unhealthy
        var health = ReadHealth(Path.Combine(options.DataDir, "scrape-summary.json"));

        // (4) raw lines of each enabled source
        var stats = new List<SourceStats>();
        foreach (var config in configs.Where(s => s.Enabled))
        {
            var entry = new SourceStats(config, health.TryGetValue(config.Id, out var healthy) && healthy);
            ReadRaw(options.DataDir, entry);
            stats.Add(entry);
        }

        var sourceKinds = new ConfigSourceKinds(configs.ToDictionary(s => s.Id, StringComparer.Ordinal));
        var canonicalKeys = new List<string>();

        await using var db = new JobRadarDbContext(
            new DbContextOptionsBuilder<JobRadarDbContext>()
                .UseSqlite(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString())
                .Options);

        // (5) prefilter, then one upsert per source, then the prefilter reason of each stored row
        foreach (var entry in stats)
        {
            foreach (var offer in entry.Offers)
            {
                var outcome = Prefilter.Evaluate(offer, criteria, blocklist);
                entry.Reasons.Add(outcome is PrefilterResult.Reject reject ? reject.ReasonId : null);
                canonicalKeys.Add(CanonicalKey.Of(offer.Company, offer.Title, offer.Country));
            }

            var upsert = await new OfferUpsert(db).UpsertAsync(entry.Offers, options.RunDate, ct);
            entry.New = upsert.New;
            entry.Changed = upsert.Changed;
            entry.Unchanged = upsert.Unchanged;
            entry.NewIds.AddRange(upsert.NewIds);
            entry.ChangedIds.AddRange(upsert.ChangedIds);

            var ids = entry.Offers.Select(o => OfferId.Of(o.Source, o.SourceId)).ToList();
            var rows = await db.Offers.Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id, ct);
            for (var i = 0; i < ids.Count; i++)
            {
                if (rows.TryGetValue(ids[i], out var row))
                {
                    row.PrefilterReason = entry.Reasons[i];
                }
            }

            await db.SaveChangesAsync(ct);
        }

        // (6) canonical groups of every ingested offer, once
        await new CanonicalGroups(db, sourceKinds).RecomputeAsync(canonicalKeys.Distinct(StringComparer.Ordinal).ToList(), ct);

        // (7) hooks, in registration order
        var context = new IngestContext(
            options.RunDate,
            stats.SelectMany(s => s.NewIds).Order(StringComparer.Ordinal).ToList(),
            stats.SelectMany(s => s.ChangedIds).Order(StringComparer.Ordinal).ToList(),
            options.DataDir);
        foreach (var hook in hooks)
        {
            await hook.RunAsync(context, ct);
        }

        // (8) missed runs and expiry, per source; only a healthy source with a complete listing counts (R8)
        var expiry = new Expiry(db);
        foreach (var entry in stats)
        {
            var seen = entry.Offers.Select(o => o.SourceId).ToHashSet(StringComparer.Ordinal);
            var missed = await expiry.RecordMissedAsync(
                entry.Config.Id, seen, entry.Healthy, entry.Config.ExpiryByAbsence, options.RunDate, ct);
            entry.Missed = missed.Missed;
            entry.Expired = missed.Expired;
        }

        // (9) archive
        var archived = await expiry.ArchiveAsync(options.RunDate, ArchiveAfterDays, ct);

        // (10) result file and the summary line
        var result = BuildResult(options.RunDate, stats, archived);
        File.WriteAllText(Path.Combine(options.DataDir, ResultFileName), JsonSerializer.Serialize(result, ResultOptions) + "\n");

        var newCount = result.NewIds.Count;
        var changedCount = result.ChangedIds.Count;
        var expired = stats.Sum(s => s.Expired);
        output.WriteLine(FormattableString.Invariant(
            $"ingest: {result.Ingested} ingested, {result.PassedPrefilter} passed prefilter (ratio {result.RatioPassedToIngested:0.####}), {newCount} new, {changedCount} changed, {expired} expired, {archived} archived"));

        return 0;
    }

    private static IngestResultFile BuildResult(DateOnly runDate, IReadOnlyList<SourceStats> stats, int archived)
    {
        var ingested = stats.Sum(s => s.Offers.Count);
        var passed = stats.Sum(s => s.Reasons.Count(r => r is null));

        var reasons = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var reason in stats.SelectMany(s => s.Reasons).Where(r => r is not null).Select(r => r!))
        {
            reasons[reason] = reasons.TryGetValue(reason, out var count) ? count + 1 : 1;
        }

        var ratio = ingested == 0 ? 0.0 : Math.Round((double)passed / ingested, 4, MidpointRounding.ToEven);

        return new IngestResultFile(
            runDate.ToString(DateFormat, CultureInfo.InvariantCulture),
            ingested,
            passed,
            ratio,
            reasons,
            stats.SelectMany(s => s.NewIds).Order(StringComparer.Ordinal).ToList(),
            stats.SelectMany(s => s.ChangedIds).Order(StringComparer.Ordinal).ToList(),
            archived,
            stats.Select(s => new SourceResultFile(
                s.Config.Id,
                s.Healthy,
                s.Offers.Count,
                s.New,
                s.Changed,
                s.Unchanged,
                s.Reasons.Count(r => r is not null),
                s.Reasons.Count(r => r is null),
                s.Errors,
                s.Missed,
                s.Expired)).ToList());
    }

    /// <summary>Reads the healthy flags of <c>scrape-summary.json</c>. A missing or unreadable file means no source is healthy.</summary>
    private static Dictionary<string, bool> ReadHealth(string path)
    {
        var health = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return health;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("sources", out var sources) || sources.ValueKind != JsonValueKind.Array)
            {
                return health;
            }

            foreach (var item in sources.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("source", out var name) || name.ValueKind != JsonValueKind.String
                    || !item.TryGetProperty("healthy", out var flag))
                {
                    continue;
                }

                var value = flag.ValueKind == JsonValueKind.True;
                var key = name.GetString()!;
                // A source listed twice is healthy only when every entry says so.
                health[key] = health.TryGetValue(key, out var previous) ? previous && value : value;
            }
        }
        catch (JsonException)
        {
            health.Clear();
        }

        return health;
    }

    /// <summary>Reads <c>raw/&lt;source&gt;.jsonl</c>. A missing file means no offers; a bad line is counted and skipped.</summary>
    private static void ReadRaw(string dataDir, SourceStats entry)
    {
        var path = Path.Combine(dataDir, "raw", entry.Config.Id + ".jsonl");
        if (!File.Exists(path))
        {
            return;
        }

        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            RawOffer? offer;
            try
            {
                offer = RawOfferJson.FromLine(line);
            }
            catch (JsonException)
            {
                offer = null;
            }

            if (offer is null || offer.Source != entry.Config.Id || !IsComplete(offer))
            {
                entry.Errors++;
                continue;
            }

            // One offer per (source, source_id): the last occurrence wins, as in OfferUpsert.
            var id = OfferId.Of(offer.Source, offer.SourceId);
            if (index.TryGetValue(id, out var at))
            {
                entry.Offers[at] = offer;
            }
            else
            {
                index[id] = entry.Offers.Count;
                entry.Offers.Add(offer);
            }
        }
    }

    private static bool IsComplete(RawOffer offer) =>
        !string.IsNullOrWhiteSpace(offer.SourceId)
        && offer.Source is not null
        && offer.Url is not null
        && offer.Title is not null
        && offer.Company is not null
        && offer.Description is not null;

    private sealed class SourceStats(SourceConfig config, bool healthy)
    {
        public SourceConfig Config { get; } = config;

        public bool Healthy { get; } = healthy;

        public List<RawOffer> Offers { get; } = [];

        /// <summary>One entry per offer of <see cref="Offers"/>: the rejecting reason id, or null when the offer passed.</summary>
        public List<string?> Reasons { get; } = [];

        public List<string> NewIds { get; } = [];

        public List<string> ChangedIds { get; } = [];

        public int Errors { get; set; }

        public int New { get; set; }

        public int Changed { get; set; }

        public int Unchanged { get; set; }

        public int Missed { get; set; }

        public int Expired { get; set; }
    }

    private sealed class ConfigSourceKinds(IReadOnlyDictionary<string, SourceConfig> configs) : ISourceKinds
    {
        public SourceKind KindOf(string source) =>
            configs.TryGetValue(source, out var config) ? config.Kind : SourceKind.Aggregator;
    }
}

/// <summary>The content of <c>ingest-result.json</c> (SPEC §13). Property order is the key order of the file.</summary>
public sealed record IngestResultFile(
    string RunDate,
    int Ingested,
    int PassedPrefilter,
    double RatioPassedToIngested,
    SortedDictionary<string, int> PrefilterReasons,
    IReadOnlyList<string> NewIds,
    IReadOnlyList<string> ChangedIds,
    int Archived,
    IReadOnlyList<SourceResultFile> Sources);

/// <summary>One source of <c>ingest-result.json</c>.</summary>
public sealed record SourceResultFile(
    string Source,
    bool Healthy,
    int Ingested,
    int New,
    int Changed,
    int Unchanged,
    int Prefiltered,
    int Passed,
    int Errors,
    int Missed,
    int Expired);
