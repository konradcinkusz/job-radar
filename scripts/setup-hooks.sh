#!/usr/bin/env bash
# Installs a pre-commit hook that runs gitleaks over staged changes (P5, repo-baseline).
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
command -v gitleaks >/dev/null || { echo "install gitleaks first: https://github.com/gitleaks/gitleaks" >&2; exit 1; }
cat > .git/hooks/pre-commit <<'HOOK'
#!/usr/bin/env bash
exec gitleaks protect --staged --redact --no-banner
HOOK
chmod +x .git/hooks/pre-commit
echo "pre-commit secret scan installed"
