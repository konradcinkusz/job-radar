using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobRadar.Core.Models;

/// <summary>JSON converter for closed enums: snake_case lower names (<c>ai_engineer</c>), numbers refused (SPEC §6.4).</summary>
public sealed class SnakeCaseEnumConverter<T>() : JsonStringEnumConverter<T>(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false)
    where T : struct, Enum;

/// <summary>Precedence for duplicates: the member with the highest value is the primary (SPEC §6.4).</summary>
[JsonConverter(typeof(SnakeCaseEnumConverter<SourceKind>))]
public enum SourceKind { Aggregator = 1, Portal = 2, Ats = 3 }

[JsonConverter(typeof(SnakeCaseEnumConverter<Lifecycle>))]
public enum Lifecycle { New, Active, Expired, Archived }

[JsonConverter(typeof(SnakeCaseEnumConverter<Liveness>))]
public enum Liveness { Unknown, Alive, Dead, Unverified }

[JsonConverter(typeof(SnakeCaseEnumConverter<WorkMode>))]
public enum WorkMode { Remote, Hybrid, Onsite, Unknown }

[JsonConverter(typeof(SnakeCaseEnumConverter<Track>))]
public enum Track { A, B, C, None }

[JsonConverter(typeof(SnakeCaseEnumConverter<SalaryPeriod>))]
public enum SalaryPeriod { Year, Month, Day, Hour }

[JsonConverter(typeof(SnakeCaseEnumConverter<RoleFamily>))]
public enum RoleFamily { AiEngineer, Backend, Fullstack, Data, Devops, Other }

[JsonConverter(typeof(SnakeCaseEnumConverter<StackMain>))]
public enum StackMain { Dotnet, Python, Java, Typescript, Go, Other, Unknown }

[JsonConverter(typeof(SnakeCaseEnumConverter<Seniority>))]
public enum Seniority { Junior, Mid, Senior, Lead, Unspecified }

[JsonConverter(typeof(SnakeCaseEnumConverter<B2bAllowed>))]
public enum B2bAllowed { Yes, No, Unknown }

[JsonConverter(typeof(SnakeCaseEnumConverter<EmployerType>))]
public enum EmployerType { Direct, Agency, Consulting, Unknown }

[JsonConverter(typeof(SnakeCaseEnumConverter<Confidence>))]
public enum Confidence { Low, Medium, High }
