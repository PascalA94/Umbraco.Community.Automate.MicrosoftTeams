#!/usr/bin/env bash
# Contract section 4, items 12 and 13: the copied credential helper must match the Jira repo's tag, and its
# CanonicalHash constant must be the frozen value for that version.
#   bash build/check-credential-helper.sh [path-to-jira-clone]   (default: clone the tag into a temp folder)
# Exit 0 only when all three files match after the four package values are put back, and the hash is right.
set -euo pipefail
cd "$(dirname "$0")/.."

TAG="credential-helper-1.0.2"
CANONICAL_HASH="4067bab2cc1fd0eb86d2f6bb717869bc44af0dae0cc7a095c206a740bd4d78cd"
JIRA_URL="https://github.com/PascalA94/Umbraco.Community.Automate.Jira"

jira="${1:-}"
if [ -z "$jira" ]; then
  jira="$(mktemp -d)"
  trap 'rm -rf "$jira"' EXIT
  git clone --quiet --depth 1 --branch "$TAG" "$JIRA_URL" "$jira"
else
  git -C "$jira" rev-parse --verify --quiet "refs/tags/$TAG" >/dev/null || { echo "Tag $TAG not found in $jira"; exit 1; }
  # Read from the tag, not the working tree, so a dirty clone cannot pass or fail the check.
  export_dir="$(mktemp -d)"; trap 'rm -rf "$export_dir"' EXIT
  git -C "$jira" archive "$TAG" | tar -x -C "$export_dir"
  jira="$export_dir"
fi

store="src/Umbraco.Community.Automate.MicrosoftTeams/CredentialHelper/TeamsCredentialStore.cs"
suites="src/Umbraco.Community.Automate.MicrosoftTeams.Tests/CredentialHelperContract"
fail=0

# Put the Jira values back in place of the four package values (contract section 2), then compare byte for byte.
sed -e 's/Umbraco:Automate:MicrosoftTeams:Credentials/Umbraco:Automate:Jira:Credentials/g' \
    -e 's/Umbraco\.Community\.Automate\.MicrosoftTeams\.Credentials/Umbraco.Community.Automate.Jira.Credentials/g' \
    -e 's/TeamsCredentialStore/JiraCredentialStore/g' \
    -e 's/TeamsCredentialPart/JiraCredentialPart/g' "$store" \
  | diff - "$jira/src/Umbraco.Community.Automate.Jira/CredentialHelper/JiraCredentialStore.cs" || fail=1

for f in CredentialHelperStoreContractTests.cs CredentialIsolationContractTests.cs; do
  diff "$suites/$f" "$jira/tests/Umbraco.Community.Automate.Jira.Tests/CredentialHelperContract/$f" || fail=1
done

actual="$(grep -o 'CanonicalHash = "[0-9a-f]*"' "$suites/CredentialHelperStoreContractTests.cs" | grep -o '[0-9a-f]\{64\}')"
if [ "$actual" != "$CANONICAL_HASH" ]; then echo "CanonicalHash is $actual, expected $CANONICAL_HASH"; fail=1; fi

if [ "$fail" -ne 0 ]; then echo "The copied credential helper has drifted from $TAG."; exit 1; fi
echo "Credential helper matches $TAG."
