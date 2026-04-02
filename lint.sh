#!/usr/bin/env bash
# Usage:
#   ./lint.sh          – build + analyzer warnings
#   ./lint.sh --fix    – auto-fix formatting with dotnet format, then build
set -euo pipefail
cd "$(dirname "$0")"

if [[ "${1:-}" == "--fix" ]]; then
    echo "==> Auto-fixing formatting..."
    dotnet format tools/lint.csproj --no-restore
fi

echo "==> Running analyzers..."
dotnet build tools/lint.csproj --nologo -v minimal
echo "==> Done."
