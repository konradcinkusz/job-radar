#!/usr/bin/env bash
# R15: writes config/criteria.lock from config/criteria.yaml.
# The version is read from the first line of criteria.yaml that starts with "version:".
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

criteria=config/criteria.yaml
lock=config/criteria.lock

version=$(awk '/^version:/ { print $2; exit }' "$criteria")
if ! [[ "$version" =~ ^[1-9][0-9]*$ ]]; then
  echo "make-criteria-lock: no positive integer 'version:' in $criteria" >&2
  exit 1
fi

hash=$(sha256sum "$criteria" | cut -d' ' -f1)
printf 'version: %s\nsha256: %s\n' "$version" "$hash" > "$lock"
echo "make-criteria-lock: wrote $lock (version $version)"
