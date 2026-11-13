# Contracts (frozen by T-01.01)

These types change only by director decision (SPEC §6.1). Code: `src/JobRadar.Core/Models`, `src/JobRadar.Core/Ports`,
`src/JobRadar.Agents/Contracts`.

## Where things live

| Type | Project | Note |
|---|---|---|
| `RawOffer`, `OfferFields`, `ScoreResult`, `Offer`, `SourceHealth`, `DiscoveredOffer`, `RawDetail`, `SourceConfig`, `CompanyRef`, `Criteria`, `TrackDefinition` | Core/Models | records, no behaviour |
| `SourceKind`, `Lifecycle`, `Liveness`, `WorkMode`, `Track`, `SalaryPeriod`, `RoleFamily`, `StackMain`, `Seniority`, `B2bAllowed`, `EmployerType`, `Confidence` | Core/Models | closed enums; JSON names are snake_case, numbers refused |
| `IJobSource`, `SourceContext`, `IKnownOffers`, `IOfferStore`, `ISourceKinds`, `IReportSink`, `IIngestHook`, `IngestContext` | Core/Ports | |
| `ISourceHttp`, `HttpResult`, `ISourceLog` | Core/Ports | **moved to Core from the Sources project** (SPEC §3.2): `SourceContext` carries them and Core cannot reference Sources. Implemented in Sources (T-01.11) |
| `IAgentRunner`, `AgentRequest`, `AgentTool`, `AgentToolResult`, `AgentRun`, `AgentUsage`, `AgentRunnerStatus`, `ToolCallRecord`, tool argument records | Agents/Contracts | SDK-free |
| `ICliCommand` | Cli/Commands | created by T-00.03 |

`ICriteria` of SPEC §6.1 is not created: the `Criteria` record is the contract (TASKS T-01.01 names no `ICriteria`).

## `RawOffer` line

One line of `raw/<source>.jsonl`, written and read by `RawOfferJson` (snake_case, unknown properties refused, `null`s written):

```json
{"source":"greenhouse","source_id":"123","url":"https://example.org/jobs/123","title":"Senior .NET Developer","company":"Acme","country":"PL","location":"Warsaw","work_mode":"hybrid","office_days_per_week":1,"contract":"b2b","salary_min":20000,"salary_max":30000,"currency":"PLN","salary_period":"month","description":"Line one\nLine two","posted_at":"2026-11-12T08:30:00+00:00","attribution":null}
```

Rules for adapters: `work_mode` is `unknown` unless the source states it; missing optional fields are `null`, never an exception;
`description` is plain text (HTML already converted); `attribution` is set only where the source's terms require it.

## Open points left for later tasks

- `IOfferStore` has `UpsertAsync` only (T-01.08); more methods are added by the director when the task that needs them exists.
- `IKnownOffers.KnownSourceIdsAsync` takes the refetch window so `scrape` can fetch detail only for new or stale ids (SPEC §6.3).
