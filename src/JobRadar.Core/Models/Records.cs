namespace JobRadar.Core.Models;

/// <summary>Result of the Extractor for one offer text (SPEC §6.4 <c>offer_fields</c>).</summary>
public sealed record OfferFields(
    RoleFamily RoleFamily,
    StackMain StackMain,
    Seniority Seniority,
    WorkMode WorkMode,
    int? OfficeDaysPerWeek,
    string? OfficeCountry,
    IReadOnlyList<string> Languages,
    B2bAllowed B2bAllowed,
    EmployerType EmployerType,
    Confidence Confidence,
    IReadOnlyDictionary<string, string> Evidence,
    string Model,
    string PromptVersion,
    DateTimeOffset ExtractedAt);

/// <summary>Result of the Scorer after hard exclusions (SPEC §6.4 <c>scores</c>).</summary>
public sealed record ScoreResult(
    Track Track,
    int Score,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> RedFlags,
    IReadOnlyList<string> CappedBy,
    int CriteriaVersion,
    string Model,
    string PromptVersion,
    DateTimeOffset ScoredAt);

/// <summary>A row of <c>offers</c> (SPEC §6.4).</summary>
public sealed record Offer(
    string Id,
    string Source,
    string SourceId,
    string Url,
    string CanonicalKey,
    string? PrimaryOfferId,
    string Title,
    string Company,
    string? Country,
    string? Location,
    WorkMode WorkMode,
    int? OfficeDaysPerWeek,
    string? Contract,
    int? SalaryMin,
    int? SalaryMax,
    string? Currency,
    SalaryPeriod? SalaryPeriod,
    string Description,
    string DescriptionHash,
    DateTimeOffset? PostedAt,
    DateOnly FirstSeenAt,
    DateOnly LastSeenAt,
    Lifecycle Lifecycle,
    int MissedRuns,
    Liveness Liveness,
    DateTimeOffset? LastCheckedAt,
    DateOnly? ExpiredAt,
    string? PrefilterReason,
    bool NeedsReview);

/// <summary>Counts and errors of one source in one run (SPEC §6.3 Health).</summary>
public sealed record SourceHealth(string Source, int OffersFound, IReadOnlyList<string> Errors);

/// <summary>One offer found in a listing; carries the raw record when the listing already contains the detail.</summary>
public sealed record DiscoveredOffer(string SourceId, string Url, string? RawJson);

/// <summary>The raw detail an adapter's <c>Normalize</c> reads. <c>Payload</c> is the JSON, XML or HTML text of one posting.</summary>
public sealed record RawDetail(string SourceId, string Url, string Payload);

/// <summary>One company of an ATS list in <c>sources.yaml</c>.</summary>
public sealed record CompanyRef(string Slug, string? Name);

/// <summary>One entry of <c>sources.yaml</c> (SPEC Appendix B).</summary>
public sealed record SourceConfig(
    string Id,
    bool Enabled,
    SourceKind Kind,
    int Stage,
    bool ExpiryByAbsence,
    IReadOnlyList<CompanyRef> Companies,
    string? Attribution,
    IReadOnlyList<string> Countries,
    bool Repairable,
    bool Browser);

/// <summary>One track of <c>criteria.yaml</c> (SPEC Appendix A).</summary>
public sealed record TrackDefinition(
    bool Enabled,
    string Label,
    IReadOnlyList<Seniority> Seniority,
    IReadOnlyList<RoleFamily> RoleFamily,
    IReadOnlyList<StackMain> StackMain,
    string? Note);

/// <summary><c>config/criteria.yaml</c> (SPEC Appendix A).</summary>
public sealed record Criteria(
    int Version,
    IReadOnlyList<string> Countries,
    int MaxOfficeDaysPerWeek,
    IReadOnlyList<string> OfficeExceptionCountries,
    int OfficePenalty,
    IReadOnlyList<string> ExcludedLanguages,
    string RequireContract,
    bool DirectEmployersOnly,
    int Threshold,
    IReadOnlyList<string> TitleKeywords,
    IReadOnlyDictionary<Track, TrackDefinition> Tracks,
    IReadOnlyList<string> InjectionPatterns);
