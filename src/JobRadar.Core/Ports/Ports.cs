using JobRadar.Core.Models;

namespace JobRadar.Core.Ports;

/// <summary>Result of one HTTP GET made by <see cref="ISourceHttp"/>.</summary>
public sealed record HttpResult(int Status, string ContentType, string Body, IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// All source requests go through this port (R9, R11). Defined in Core because <see cref="SourceContext"/> carries it and Core
/// cannot reference Sources; implemented in Sources (T-01.11).
/// </summary>
public interface ISourceHttp
{
    Task<HttpResult> GetAsync(Uri url, CancellationToken ct);

    /// <summary>POST with a text body (No Fluff Jobs search, SOURCES.md).</summary>
    Task<HttpResult> PostAsync(Uri url, string contentType, string body, CancellationToken ct);
}

/// <summary>Logging port for adapters (Core takes no logging package). Never pass a token, a response body or more than 200 characters of a description.</summary>
public interface ISourceLog
{
    void Info(string message);

    void Warn(string message);
}

/// <summary>What an adapter receives: HTTP, the clock, its own config and a log (SPEC §6.3).</summary>
public sealed record SourceContext(ISourceHttp Http, TimeProvider Clock, SourceConfig Config, ISourceLog Log);

/// <summary>One adapter per source (SPEC §6.3). Frozen by T-01.01.</summary>
public interface IJobSource
{
    string Id { get; }

    /// <summary>Ats = 3, Portal = 2, Aggregator = 1 (precedence for duplicates).</summary>
    SourceKind Kind { get; }

    Task<IReadOnlyList<DiscoveredOffer>> DiscoverAsync(SourceContext context, CancellationToken ct);

    Task<RawDetail?> FetchDetailAsync(DiscoveredOffer offer, SourceContext context, CancellationToken ct);

    /// <summary>Deterministic, no model, no I/O.</summary>
    RawOffer Normalize(RawDetail detail);

    SourceHealth Health();
}

/// <summary>Read-only view of known offers, used by scrape to fetch detail only for new or stale ids (SPEC §6.3).</summary>
public interface IKnownOffers
{
    /// <summary>Source ids already stored for <paramref name="source"/> and seen within the last <paramref name="refetchAfterDays"/> days of <paramref name="asOf"/>.</summary>
    Task<IReadOnlySet<string>> KnownSourceIdsAsync(string source, DateOnly asOf, int refetchAfterDays, CancellationToken ct);
}

public sealed record UpsertResult(int New, int Changed, int Unchanged, IReadOnlyList<string> NewIds, IReadOnlyList<string> ChangedIds);

/// <summary>The writer of offers; implemented by Store only (R13).</summary>
public interface IOfferStore
{
    Task<UpsertResult> UpsertAsync(IReadOnlyList<RawOffer> offers, DateOnly runDate, CancellationToken ct);
}

/// <summary>Resolves the <see cref="SourceKind"/> of a source id so Store does not read config (T-01.09).</summary>
public interface ISourceKinds
{
    SourceKind KindOf(string source);
}

/// <summary>Where the composed report goes (the publish stage).</summary>
public interface IReportSink
{
    Task WriteAsync(DateOnly runDate, string reportMarkdown, string issueBody, CancellationToken ct);
}

public sealed record IngestContext(DateOnly RunDate, IReadOnlyList<string> NewIds, IReadOnlyList<string> ChangedIds, string DataDir);

/// <summary>Runs after the upsert in <c>ingest</c>; T-02.14 adds the feedback loader as one.</summary>
public interface IIngestHook
{
    Task RunAsync(IngestContext context, CancellationToken ct);
}
