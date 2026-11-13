#!/usr/bin/env bash
# One request per source (T-00.04). Slugs come from the environment, never interpolated into the shell by the workflow.
# ATS: keep the body trimmed to 5 postings. Others: keep only status, content type, size and top-level/posting field names.
set -uo pipefail
out=${1:-probe-out}; mkdir -p "$out"
summary="$out/summary.tsv"; printf 'source\tstatus\tcontent_type\tbytes\turl\n' > "$summary"

fetch() { # id url [curl args...]
  local id=$1 url=$2; shift 2
  local meta
  meta=$(curl -sS -m 30 -A "$UA" -D "$out/$id.headers" -o "$out/$id.raw" -w '%{http_code}\t%{content_type}\t%{size_download}' "$@" "$url" 2>"$out/$id.err") || meta=$'000\t\t0'
  printf '%s\t%s\t%s\n' "$id" "$meta" "$url" >> "$summary"
}
trim_ats() { # id jq-filter
  if jq -e . "$out/$1.raw" >/dev/null 2>&1; then jq "$2" "$out/$1.raw" > "$out/$1.json"; else echo "not json" > "$out/$1.note"; fi
  rm -f "$out/$1.raw"
}
shape() { # id  (field names only, no values)
  if jq -e . "$out/$1.raw" >/dev/null 2>&1; then
    jq '[.. | objects | keys[]] | unique' "$out/$1.raw" > "$out/$1.fields.json"
  else
    head -c 600 "$out/$1.raw" | sed -E 's/>[^<]{40,}</>…</g' | head -c 300 > "$out/$1.head.txt"
  fi
  rm -f "$out/$1.raw"
}

fetch greenhouse "https://boards-api.greenhouse.io/v1/boards/${GH_SLUG}/jobs?content=true"
trim_ats greenhouse '{jobs: (.jobs[:5])}'
fetch ashby "https://api.ashbyhq.com/posting-api/job-board/${ASHBY_SLUG}?includeCompensation=true"
trim_ats ashby '{jobs: (.jobs[:5])}'
fetch lever "https://api.lever.co/v0/postings/${LEVER_SLUG}?mode=json&limit=5"
trim_ats lever '.[:5]'
fetch remotive "https://remotive.com/api/remote-jobs?limit=5"
shape remotive
fetch weworkremotely "https://weworkremotely.com/remote-jobs.rss"
shape weworkremotely
fetch nofluffjobs "https://nofluffjobs.com/api/search/posting?salaryCurrency=PLN&salaryPeriod=month&region=pl" -X POST -H 'Content-Type: application/infiniteSearch+json' --data '{"rawSearch":"backend","page":1}'
shape nofluffjobs
fetch justjoinit "https://api.justjoin.it/v2/user-panel/offers/by-cursor?currencies=pln&orderBy=DESC&sortBy=published&perPage=5"
shape justjoinit

column -t -s $'\t' "$summary"
