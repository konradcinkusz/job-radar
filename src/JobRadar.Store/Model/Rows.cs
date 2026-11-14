namespace JobRadar.Store.Model;

// Entities for the SPEC §6.4 tables. Column names come from the context (snake_case of the property name).
// Enum-like columns are plain strings: the repositories of later tasks convert them.
// Timestamps are ISO 8601 UTC text; JSON columns are text. Nullability: `string?` / `int?` = NULL allowed.

/// <summary>A row of <c>offers</c>.</summary>
public sealed class OfferRow
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string CanonicalKey { get; set; } = string.Empty;
    public string? PrimaryOfferId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Location { get; set; }
    public string WorkMode { get; set; } = string.Empty;
    public int? OfficeDaysPerWeek { get; set; }
    public string? Contract { get; set; }
    public int? SalaryMin { get; set; }
    public int? SalaryMax { get; set; }
    public string? Currency { get; set; }
    public string? SalaryPeriod { get; set; }
    public string Description { get; set; } = string.Empty;
    public string DescriptionHash { get; set; } = string.Empty;
    public string? PostedAt { get; set; }
    public string FirstSeenAt { get; set; } = string.Empty;
    public string LastSeenAt { get; set; } = string.Empty;
    public string Lifecycle { get; set; } = string.Empty;
    public int MissedRuns { get; set; }
    public string Liveness { get; set; } = string.Empty;
    public string? LastCheckedAt { get; set; }
    public string? ExpiredAt { get; set; }
    public string? PrefilterReason { get; set; }
    public bool NeedsReview { get; set; }
}

/// <summary>A row of <c>offer_fields</c>: fields the Extractor produced for one description version.</summary>
public sealed class OfferFieldsRow
{
    public string OfferId { get; set; } = string.Empty;
    public string DescriptionHash { get; set; } = string.Empty;
    public string RoleFamily { get; set; } = string.Empty;
    public string StackMain { get; set; } = string.Empty;
    public string Seniority { get; set; } = string.Empty;
    public string WorkMode { get; set; } = string.Empty;
    public int? OfficeDaysPerWeek { get; set; }
    public string? OfficeCountry { get; set; }
    public string Languages { get; set; } = string.Empty;
    public string B2bAllowed { get; set; } = string.Empty;
    public string EmployerType { get; set; } = string.Empty;
    public string Confidence { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string ExtractedAt { get; set; } = string.Empty;
}

/// <summary>A row of <c>scores</c>. A new criteria version adds a row; it replaces none.</summary>
public sealed class ScoreRow
{
    public string OfferId { get; set; } = string.Empty;
    public string DescriptionHash { get; set; } = string.Empty;
    public string CriteriaVersion { get; set; } = string.Empty;
    public string Track { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Reasons { get; set; } = string.Empty;
    public string RedFlags { get; set; } = string.Empty;
    public string CappedBy { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string ScoredAt { get; set; } = string.Empty;
}

/// <summary>A row of <c>runs</c>. <c>FinishedAt</c> is NULL while a run is in progress or when it was interrupted.</summary>
public sealed class RunRow
{
    public string RunId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string StartedAt { get; set; } = string.Empty;
    public string? FinishedAt { get; set; }
    public int OffersFound { get; set; }
    public int OffersNew { get; set; }
    public int OffersChanged { get; set; }
    public string Errors { get; set; } = string.Empty;
    public bool Healthy { get; set; }
}

/// <summary>A row of <c>agent_usage</c>. Credits and USD are NULL when the runtime reported none.</summary>
public sealed class AgentUsageRow
{
    public string RunId { get; set; } = string.Empty;
    public string Agent { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Batches { get; set; }
    public int Offers { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int ReasoningTokens { get; set; }
    public int CachedInputTokens { get; set; }
    public double? AiCredits { get; set; }
    public double? Usd { get; set; }
}

/// <summary>A row of <c>feedback</c>, filled by ingest from <c>feedback/feedback.jsonl</c>.</summary>
public sealed class FeedbackRow
{
    public string OfferId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string Verdict { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string Author { get; set; } = string.Empty;
}
