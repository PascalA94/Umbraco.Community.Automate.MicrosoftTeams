#!/usr/bin/env bash
# Posts one payload to the Workflows webhook in $TEAMS_SPIKE_URL and prints a safe summary: the status code, the body
# length, the first 200 body characters with URLs and sig= values redacted, and the URL host (so AllowedHosts can be set).
# It never echoes the URL, never runs with `set -x`, and passes the URL to curl on stdin so it is not in the process list.
# Usage: read -rs TEAMS_SPIKE_URL && export TEAMS_SPIKE_URL; ./spikes/post.sh spikes/payloads/bare-text.json
set +x
set -euo pipefail

payload="${1:?usage: post.sh <payload.json>}"
[ -f "$payload" ] || { echo "post.sh: no such file: $payload" >&2; exit 2; }
[ -n "${TEAMS_SPIKE_URL:-}" ] || { echo "post.sh: TEAMS_SPIKE_URL is not set" >&2; exit 2; }

body="$(mktemp)"
trap 'rm -f "$body"' EXIT

redact() { sed -E 's/sig=[^&" ]+/sig=[redacted]/g; s#https?://[^ "]+#[redacted]#g; s/(host|resolve host): [^ ]+/\1: [redacted]/g'; }

# A transport failure must still print a (redacted) line, so errors are captured instead of ending the script.
status="$(printf 'url = "%s"\n' "$TEAMS_SPIKE_URL" | curl -sS -K - -X POST \
  -H 'Content-Type: application/json' --data-binary "@$payload" \
  -o "$body" -w '%{http_code}' 2>&1 | redact || true)"

echo "payload: $payload"
echo "status: $status"
echo "body length: $(wc -c < "$body" | tr -d ' ')"
echo "body start: $(head -c 200 "$body" | tr '\n' ' ' | redact)"
echo "host: $(printf '%s' "$TEAMS_SPIKE_URL" | awk -F/ '{print $3}')"
