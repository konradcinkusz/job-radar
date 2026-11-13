using System.Text.Json;

namespace JobRadar.Core.Models;

/// <summary>
/// The common record an adapter produces from deterministic fields, no model involved. One line of <c>raw/&lt;source&gt;.jsonl</c> (SPEC §0, §6.3).
/// Frozen by T-01.01.
/// </summary>
public sealed record RawOffer(
    string Source,
    string SourceId,
    string Url,
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
    DateTimeOffset? PostedAt,
    string? Attribution);

/// <summary>The one JSON shape of a raw offer line: snake_case names, closed enums, nulls kept.</summary>
public static class RawOfferJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false,
    };

    public static string ToLine(RawOffer offer) => JsonSerializer.Serialize(offer, Options);

    public static RawOffer? FromLine(string line) => JsonSerializer.Deserialize<RawOffer>(line, Options);
}
