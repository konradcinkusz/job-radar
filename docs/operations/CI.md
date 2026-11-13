# CI: what each job needs and takes

Measured values are filled from workflow runs (links in the PR that last changed this file). Empty cells are not yet measured.

| Job | Proves | Downloads at run time | Needs from the runner | Duration (measured) |
|---|---|---|---|---|
| `build-test` | build with `-warnaserror`, tests `tier!=live&tier!=browser` | .NET SDK 10.0.x (`actions/setup-dotnet`), NuGet packages, and **the Copilot CLI (~89 MB, v1.0.105 linux-x64) fetched by the `GitHub.Copilot.SDK` package build step**: observed in a local build, source github.com release | .NET SDK, outbound HTTPS to nuget.org and github.com | |
| `kernel-size` | `JobRadar.ServiceDefaults` ≤ 400 lines | nothing | bash | |
| `solution-check` | solution membership, package rules | nothing | bash | |
| `secret-scan` | gitleaks over commits reachable from `HEAD` | the gitleaks binary (action) | outbound HTTPS | |

## Notes

- Self-hosted switch: repository variable `RUNNER_LABELS` (JSON array), unset by default (DECISIONS D-09).
- The Copilot CLI download happens during the build of `JobRadar.Agents` (measured locally on 2026-11-13, not yet on a runner). If a runner has no access to github.com the build fails there; this is relevant to SPEC A-03.
