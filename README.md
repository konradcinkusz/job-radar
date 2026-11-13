# job-radar

Daily pipeline (GitHub Actions + Playwright + GitHub Copilot SDK agents + SQLite) that collects IT job offers, de-duplicates and
checks them, and reports the ones that match written criteria. Specification: `konradcinkusz/job-radar-spec`.

**State:** scaffold only. `dotnet build JobRadar.sln -warnaserror` and the tests pass; `jobradar doctor` prints a placeholder line.
Nothing else is implemented. Measured state is tracked in `PROGRESS.md` of the specification repository.

```bash
dotnet build JobRadar.sln -warnaserror
dotnet test JobRadar.sln --filter "tier!=live&tier!=browser"
dotnet run --project src/JobRadar.Cli -- doctor
```
