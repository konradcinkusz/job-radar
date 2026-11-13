# AGENTS.md

JobRadar application repository (private). The specification lives in `konradcinkusz/job-radar-spec`
(`docs/spec/SPEC.md`, `SOURCES.md`, `COPILOT-SDK.md`, `TASKS.md`, `DECISIONS.md`). Do not guess: if the task is unclear, write the
question in the PR description under "Blocked: question for director".

- Verify: `dotnet build JobRadar.sln -warnaserror && dotnet test JobRadar.sln --filter "tier!=live&tier!=browser"`, then `scripts/check-solution.sh && scripts/check-kernel-size.sh`.
- Package versions live only in `Directory.Packages.props`; shared files (`*.csproj`, `JobRadar.sln`, `.github/workflows/**`, `src/JobRadar.AppHost/**`) are director-only.
- Only `JobRadar.Agents` references `GitHub.Copilot.SDK`; only `JobRadar.Sources` references `Microsoft.Playwright`.
- No secrets, no absolute paths, fixtures at most 5 postings. English for code and docs; report labels are Polish.
- Standards: `konradcinkusz/architecture-standards` (P1–P15), declared in `.claude/settings.json`.
