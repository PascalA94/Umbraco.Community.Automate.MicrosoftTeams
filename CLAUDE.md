# CLAUDE.md

`Umbraco.Community.Automate.MicrosoftTeams`: Post messages and Adaptive Cards to Microsoft Teams from Umbraco Automate, through a Workflows webhook.
Plain class library in `src/Umbraco.Community.Automate.MicrosoftTeams/` (no Razor, no client assets). Both lines compile the same source; a gap goes behind `#if AUTOMATE_17` (none needed so far).
Design `docs/design.md`, plan `docs/plan.md`, spikes `docs/spikes.md`, if present, local notes `COMMITS-TODO.md` and `HANDOVER.md` (gitignored, not in a fresh clone).

**Never commit, push or publish.** Pascal commits, pushes, tags and releases.

Portfolio token rules and CI rules are kept outside this repo, in the package workspace README ("Token rules", "CI and GitHub rules").

## Lines and versions

- **One branch, two lines.** `AutomateMajor` (`17` or `18`, default `18`, set as a property or an environment variable) picks the CMS range (`[17.8.0, 18.0.0)` or `[18.3.0, 19.0.0)`), the package version (`17.x.y` or `18.x.y`), the `AUTOMATE_17` / `AUTOMATE_18` constants and its own `packages.<line>.lock.json`, `obj/<line>` and `bin/<line>`. `eng/lines.txt` lists the lines; `bash build/test.sh` runs the suites once per line.
- Both lines build from `main`; there is no `v17` branch. Floors and ranges live in `Directory.Build.props`; the CI legs and the tag rule are in `.github/workflows/`.
- A version bump touches every file `build/check-release-version.sh` checks, together.
- The NuGet ID, aliases, settings property names, and any migration plan, table or route names are permanent once released.

## Commands

```bash
bash build/test.sh       # restore, build and test every line in eng/lines.txt (SQLite); or bash build/test.sh 17
dotnet restore src/Umbraco.Community.Automate.MicrosoftTeams.slnx -p:AutomateMajor=17 --locked-mode   # one line, locked
# SQL Server integration run: set Tests__Database__DatabaseType=SqlServer and Tests__Database__SQLServerMasterConnectionString
# Demo site: HTTPS only (OpenIddict refuses HTTP)
dotnet user-secrets set "Umbraco:CMS:Unattended:UnattendedUserPassword" "<pw>" --project demo/Umbraco.Community.Automate.MicrosoftTeams.DemoSite
dotnet dev-certs https && dotnet run --project demo/Umbraco.Community.Automate.MicrosoftTeams.DemoSite -p:AutomateMajor=17   # https://localhost:44331/umbraco; use a separate database per line
# e2e (demo running; env vars as in tests/e2e/.env.example)
cd tests/e2e && npm ci --ignore-scripts && npx playwright install chromium && npx playwright test --reporter=line
bash build/check-vulnerable.sh
```

## Releasing

- Bump `PackageMinor`, `PackagePatch` and `PackageVersionSuffix` in `Directory.Build.props` (both lines move together).
- `bash build/check-vulnerable.sh` and `bash build/check-release-version.sh release/<minor>.<patch>[-suffix]` both exit 0.
- While `eng/live-gate-open` exists the suffix must be `beta.N` (`build/check-live-gate.sh`).
- Run the **Release checks** workflow by hand on the release branch (SQL Server integration, and e2e if present). Never on PRs. Re-run after a new Umbraco minor.
- Squash-merge, tag `release/<minor>.<patch>[-suffix]` on the merge commit; `publish.yml` (tag `release/*`, from `main` only) restores, builds, tests and audits both lines, packs them with `bash build/pack.sh` (one nupkg per line) and publishes with Trusted Publishing. Then `gh release create` per line, and mark the newest Umbraco line's release as Latest.
- Stable from the first real public release unless the API is still moving (`.github/REPO-SETUP.md` step 10); after that, no breaking changes within a major.
- "Done" means green on GitHub. The first CI run is its own step with Pascal.

## Keep

- Floors never below 17.8.0 / 18.3.0. The repo is public: no reviews, plans of record or local paths in it.
- `docs/` holds only `design.md`, `plan.md`, `spikes.md`. Fake test secrets carry `// gitleaks:allow` from their first commit.
