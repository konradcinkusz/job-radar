# CI: what each job needs and takes

Measured values are filled from workflow runs (links in the PR that last changed this file). Empty cells are not yet measured.

| Job | Proves | Downloads at run time | Needs from the runner | Duration (measured) |
|---|---|---|---|---|
| `build-test` | build with `-warnaserror`, tests `tier!=live&tier!=browser` | .NET SDK 10.0.x (`actions/setup-dotnet`), NuGet packages, and **the Copilot CLI (~89 MB, v1.0.105 linux-x64) fetched by the `GitHub.Copilot.SDK` package build step**: observed in a local build, source github.com release | .NET SDK, outbound HTTPS to nuget.org and github.com | 51 s (build step 41 s, test step 3 s) |
| `kernel-size` | `JobRadar.ServiceDefaults` ≤ 400 lines | nothing | bash | 5 s |
| `solution-check` | solution membership, package rules | nothing | bash | 4 s |
| `secret-scan` | gitleaks over commits reachable from `HEAD` | the gitleaks binary (action) | outbound HTTPS | 7 s |

Durations: attempt 3 of https://github.com/konradcinkusz/job-radar/actions/runs/43385096931 (commit `037f890`, GitHub-hosted `ubuntu-latest`, measured 2026-11-13). Attempts 1 and 2 of the same run were also green (build-test 45 s in attempt 1).

## Notes

- Self-hosted switch: repository variable `RUNNER_LABELS` (JSON array), unset by default (DECISIONS D-09).
- The Copilot CLI download happens during the build of `JobRadar.Agents` (measured locally on 2026-11-13). On a GitHub-hosted runner the build step succeeded (41 s) in the run above, so the download did not block the build there; whether it happened on the runner was not read from the log. If a runner has no access to github.com the build may fail there; relevant to SPEC A-03.
