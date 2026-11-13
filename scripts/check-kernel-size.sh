#!/usr/bin/env bash
# SPEC §9 kernel-size: JobRadar.ServiceDefaults is at most 400 lines of C#.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
n=$(find src/JobRadar.ServiceDefaults -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -print0 | xargs -0 cat | wc -l)
echo "kernel-size: $n lines (limit 400)"
[ "$n" -le 400 ] || { echo "kernel-size: ServiceDefaults has $n lines, limit is 400" >&2; exit 1; }
