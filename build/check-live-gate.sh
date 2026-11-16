#!/usr/bin/env bash
# Release guard for the mock-first build (plan, "Mock-first, live gate deferred"). While eng/live-gate-open exists, a tag
# must be a -beta.N prerelease and README.md must say the package is not yet verified against live Teams. Removing the
# marker is one reviewed commit, made when Pascal has run the Live gate.
# Usage: bash build/check-live-gate.sh release/0.0-beta.1   (run from the repository root)
set -euo pipefail
cd "$(dirname "$0")/.."

tag="${1:?usage: check-live-gate.sh <release/minor.patch[-suffix]>}"

if [ ! -f eng/live-gate-open ]; then
  echo "check-live-gate: ok (the Live gate is closed, no restriction)"
  exit 0
fi

status=0
case "$tag" in
  *-beta.*) ;;
  *) echo "check-live-gate: '$tag' is not a -beta.N tag, and the Live gate has not passed (eng/live-gate-open exists)" >&2; status=1 ;;
esac
if ! grep -q "not yet verified against live Teams" README.md; then
  echo "check-live-gate: README.md lacks 'not yet verified against live Teams' while the Live gate is open" >&2
  status=1
fi
[ "$status" -eq 0 ] && echo "check-live-gate: ok ('$tag' is a beta and the README carries the warning)"
exit $status
