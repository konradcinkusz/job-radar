# Spikes

## Real Copilot session, run locally (T-00.05, owner)

Needs your Copilot-enabled GitHub account and a fine-grained token with only the "Copilot Requests" permission.
Never commit the token and never paste it into chat.

```bash
export COPILOT_GITHUB_TOKEN=...   # your token
dotnet run --project src/JobRadar.Cli -- spike
# optional: --model <id> (one of the ids printed by the first run)
```

What it does: starts a client in `CopilotClientMode.Empty`, prints the sign-in state and the model list, runs one session with a
single custom tool `save_probe` (terminal, no permission prompt) and a handler that rejects every permission request, then asks the
model to run a shell command and prints what happened (permission requests, tool calls, usage and credits).

Send the whole console output to the director, plus the credits shown on your billing page for that run if you can see them.
`dotnet run --project src/JobRadar.Cli -- spike --fake` runs the same program on scripted output without any token (this is what CI runs).

## Source probes (T-00.04)

`probe.yml` (workflow_dispatch) makes one request per source from a hosted runner and uploads `probe-out` for 7 days. The repository
is public, so only ATS responses (trimmed to 5 postings) are stored; aggregator and portal responses keep status, content type,
size and JSON field names only.
