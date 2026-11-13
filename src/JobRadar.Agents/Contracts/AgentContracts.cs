using JobRadar.Core.Models;

namespace JobRadar.Agents.Contracts;

/// <summary>SDK-free (SPEC §6.5). Frozen by T-01.01.</summary>
public enum AgentRunnerStatus { Ready, Fake, NotAuthenticated, Unavailable }

public interface IAgentRunner
{
    AgentRunnerStatus Status { get; }

    Task<AgentRun> RunAsync(AgentRequest request, CancellationToken ct);
}

/// <summary>A tool the agent may call. The schema is derived from <paramref name="ArgumentsType"/>, a closed record.</summary>
public sealed record AgentTool(
    string Name,
    string Description,
    Type ArgumentsType,
    Func<object, AgentToolResult> Handler,
    bool EndsTurn);

/// <summary>A rejection is shown to the model so it can retry within the session.</summary>
public sealed record AgentToolResult(bool Accepted, string Message);

public sealed record AgentRequest(
    string AgentName,
    string Model,
    string? ReasoningEffort,
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyList<AgentTool> Tools,
    TimeSpan Timeout);

public sealed record ToolCallRecord(string ToolName, string ArgumentsJson, bool Accepted, string Message);

/// <summary><c>AiCredits</c> is null when the runtime reported none (never a zero, SPEC §5 R14).</summary>
public sealed record AgentUsage(int InputTokens, int OutputTokens, int ReasoningTokens, int CachedInputTokens, double? AiCredits);

public sealed record AgentRun(IReadOnlyList<ToolCallRecord> ToolCalls, string? FinalText, AgentUsage Usage, TimeSpan Latency);

// Tool argument records: closed shapes, no object / JsonElement / dictionaries (COPILOT-SDK.md §5).

public sealed record EvidenceQuotes(string? WorkMode, string? Office, string? B2b, string? EmployerType, string? Languages);

public sealed record OfferFieldsEntry(
    string Ref,
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
    EvidenceQuotes Evidence);

public sealed record SaveOfferFieldsArgs(IReadOnlyList<OfferFieldsEntry> Offers);

public sealed record ScoreEntry(string Ref, Track Track, int Score, IReadOnlyList<string> Reasons, IReadOnlyList<string> RedFlags);

public sealed record SaveScoreArgs(IReadOnlyList<ScoreEntry> Scores);

public sealed record WriteReportSummaryArgs(string SummaryMd);

public sealed record ReadAdapterFileArgs(string Path);

public sealed record WriteAdapterFileArgs(string Path, string Content);

public sealed record RunAdapterTestsArgs;
