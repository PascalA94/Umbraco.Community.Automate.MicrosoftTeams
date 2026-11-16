#!/usr/bin/env bash
# Packs one nupkg per Umbraco line in eng/lines.txt into <outdir> (default ./artifacts).
# Usage: bash build/pack.sh [outdir]. Run from the repository root, after the client build and the tests.
set -euo pipefail
cd "$(dirname "$0")/.."
out="${1:-./artifacts}"
proj=src/Umbraco.Community.Automate.MicrosoftTeams/Umbraco.Community.Automate.MicrosoftTeams.csproj
mkdir -p "$out"

while read -r line; do
  [ -n "$line" ] || continue
  version="$(dotnet msbuild "$proj" "-p:AutomateMajor=$line" -getProperty:Version | tr -d '[:space:]')"
  case "$version" in ''|*-) echo "invalid package version '$version' for line $line" >&2; exit 1;; esac
  dotnet restore "$proj" "-p:AutomateMajor=$line" --locked-mode -v q
  dotnet build "$proj" -c Release --no-restore "-p:AutomateMajor=$line" -v q
  dotnet pack "$proj" -c Release --no-build "-p:AutomateMajor=$line" -o "$out" -v q
  echo "packed $line: $version"
done < eng/lines.txt
