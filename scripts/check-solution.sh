#!/usr/bin/env bash
# SPEC §9 solution-check.
#  1. every .csproj under src/ and tests/ is in JobRadar.sln
#  2. only Agents.csproj references GitHub.Copilot.SDK
#  3. only Sources.csproj (and a browser test project) references Microsoft.Playwright
#  4. every exe project is registered in the AppHost (Cli is the only one)
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
status=0
fail() { echo "solution-check: $*" >&2; status=1; }

while IFS= read -r proj; do
  name=$(basename "$proj")
  grep -q "$name" JobRadar.sln || fail "$proj is not in JobRadar.sln"
done < <(find src tests -name '*.csproj' | sort)

while IFS= read -r proj; do
  [ "$(basename "$proj")" = "JobRadar.Agents.csproj" ] || fail "$proj references GitHub.Copilot.SDK (only Agents may)"
done < <(grep -l 'Include="GitHub.Copilot.SDK"' $(find src tests -name '*.csproj') 2>/dev/null || true)

while IFS= read -r proj; do
  case "$(basename "$proj")" in
    JobRadar.Sources.csproj|JobRadar.Sources.BrowserTests.csproj) ;;
    *) fail "$proj references Microsoft.Playwright (only Sources and the browser test project may)";;
  esac
done < <(grep -l 'Include="Microsoft.Playwright"' $(find src tests -name '*.csproj') 2>/dev/null || true)

grep -q 'JobRadar.Cli.csproj' src/JobRadar.AppHost/JobRadar.AppHost.csproj || fail "AppHost does not reference JobRadar.Cli"

[ "$status" -eq 0 ] && echo "solution-check: ok"
exit "$status"
