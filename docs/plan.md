# Automate Microsoft Teams Connector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task (Pascal's build approach for Teams: **native, plus one final whole-branch review**, Task 19). Steps use checkbox (`- [ ]`) syntax for tracking.

**S7 gate result (Jira Task 0):** PENDING. Jira Task 0 step 9 replaces this line with the row, the date and a link to Jira's `docs/spikes.md`. M1 (Task 6) does not start while it says PENDING.

**Mock-first (Pascal, 2026-11-16):** there is no Microsoft 365 tenant. Build against a local mock (Task 4a). Task 4's live posts and Task 21 move to the deferred **Live gate**, run when a tenant exists. Only `-beta.N` versions until it passes. See "Mock-first, live gate deferred (Pascal, 2026-11-16)" at the end. It supersedes any "no tenant, don't start" text above.

**Goal:** Ship `Umbraco.Community.Automate.MicrosoftTeams` 17.0.0 and 18.0.0: one Workflows webhook connection (backed by a package-owned credential) and two actions (Post message, Post Adaptive Card with four built-in templates) for Umbraco Automate.

**Architecture:** A plain `Microsoft.NET.Sdk` class library laid out like HQ's `Umbraco.Automate.Slack`. Connection and actions are found by attribute, with UI from `[Field]` POCOs. Cards are built as `System.Text.Json` node trees, so bound data can't break the JSON. The connection stores only a credential name; the webhook URL lives in server config under `Umbraco:Automate:MicrosoftTeams:Credentials:{name}:WebhookUrl`, read only through `TeamsCredentialStore`, a verbatim copy of Jira's store under the shared credential-helper contract. One `ITeamsWebhookSender` singleton owns credential lookup, URL checks, the rate limiter, the size check, the POST and error mapping, so the webhook URL has exactly one code path. Data-driven problems are fixed in the payload with a warning, never by failing the step (circuit breaker, F01). One branch builds both majors through Jira's `AutomateMajor=17|18` conventions, so Jira's CI files run unchanged.

**Tech Stack:** .NET 10 (`net10.0`), Umbraco.Cms.Core 17.8.0+ / 18.3.0+, Umbraco.Automate.Core 17.5.1+ / 18.5.1+, xUnit, Umbraco.Automate.Testing, JsonSchema.Net and Microsoft.Extensions.TimeProvider.Testing (test-only), GitHub Actions (Jira's workflows), gitleaks, Semgrep.

**Spec:** `docs/design.md`. The credential helper's spec is the credential-helper contract (1.0.2, kept in the package workspace, not in this repo); where the two disagree, the contract wins. Background scope notes also stay outside the repo. Where the spec and the scope notes disagree, the spec wins.

**Updated 2026-10-30** for the independent plan review (`plan-review.md`, P1 to P15) and Pascal's decisions. Tasks are renumbered; the old-to-new map is in "Plan review changes (2026-10-30)" at the end.

**Repo:** `PascalA94/Umbraco.Community.Automate.MicrosoftTeams` (public). Below, `SRC` = `src/Umbraco.Community.Automate.MicrosoftTeams` and `TESTS` = `tests/Umbraco.Community.Automate.MicrosoftTeams.Tests.Unit`. Root namespace `Umbraco.Community.Automate.MicrosoftTeams`. Jira repo (copy source): `PascalA94/Umbraco.Community.Automate.Jira`, tag `credential-helper-1.0.2`.

**Layout reference (read-only):** HQ's Slack add-on (`Umbraco.Automate.Slack`, HQ repo at `v18/dev`, 8 Oct 2026). Core API sources sit next to it under `Umbraco.Automate/src`. The same files are on GitHub.

**Estimate (architect's, 2026-10-30, plan review P14):** about 6 to 8 working days of AI-assisted build: M0 up to 2 (time-boxed), M1 2 to 3, M2 about 1.5, M3 about 1, M4 about 1 (Pascal-led). On top: waiting for Jira Task 0, and the contract freeze before release.

## Global Constraints

- Aliases, frozen at first release: connection `community.teams.workflowWebhook`, actions `community.teams.postMessage` and `community.teams.postCard`. Settings property names and built-in template keys are frozen too.
- Display names: "Microsoft Teams (Workflows webhook)", "Post Teams message", "Post Teams Adaptive Card". Group "Messaging", icon `icon-message`.
- `net10.0` for both majors. `AutomateMajor=17`: `Umbraco.Cms.Core [17.8.0, 18.0.0)` and `Umbraco.Automate.Core [17.5.1, 18.0.0)`; `AutomateMajor=18`: `Umbraco.Cms.Core [18.3.0, 19.0.0)` and `Umbraco.Automate.Core [18.5.1, 19.0.0)`. The `Umbraco.Cms.Core` reference exists only to enforce the portfolio CMS floor (spec section 4). `Umbraco.Automate.Testing` uses the Automate ranges.
- **Build conventions are Jira's** (Jira plan Task 1 step 2), so Jira's scripts and workflows run unchanged: `AutomateMajor` default `18`; `DefineConstants` `AUTOMATE_$(AutomateMajor)` (so `AUTOMATE_17` / `AUTOMATE_18`, never `AUTOMATE17`); `eng/lines.txt`; `scripts/test.sh`; `AutomateFloat=true` for the CI latest leg; lock files `packages.$(AutomateMajor).lock.json`; `VersionPrefix = $(AutomateMajor).$(PackageMinor).$(PackagePatch)`; releases from a `release/<minor>.<patch>[-<suffix>]` tag.
- Options section `Umbraco:Automate:MicrosoftTeams`: `AllowedHosts` default `*.powerplatform.com`, `*.logic.azure.com`, `*.azure-apihub.net` (Task 4 may replace these; mock-first: they come from `TeamsWorkflowsFacts` and the Live gate may replace them); `StrictHosts = false`; `AllowedLinkHosts = []`; `MaxPayloadBytes = 28000`; `MaxValueLength = 2000`; `TimeoutSeconds = 15`; `RateLimit: { RequestsPerSecond = 3, Burst = 4, MaxWaitSeconds = 5 }`.
- **Credentials follow the contract verbatim.** Store `TeamsCredentialStore`, prefix `Umbraco:Automate:MicrosoftTeams:Credentials`, parts enum `TeamsCredentialPart { WebhookUrl }`, namespace `Umbraco.Community.Automate.MicrosoftTeams.Credentials`. Name rule `^[a-z0-9]{1,32}\z` after trim, ASCII check and lower-casing. The store file and both contract test files are copied from the Jira repo at tag `credential-helper-1.0.0` with only the four section-2 tokens changed, never edited in place (contract section 6). Only `TeamsWebhookSender` calls `TryGet`. Never bound to `TeamsOptions`.
- **Gate:** M1 (Task 6 onward) starts only when Jira Task 0 is recorded GO (any go row) and the contract status is **Frozen**. Teams never writes the helper first. Jira's NO-GO row switches this plan to the "NO-GO delta" section.
- **No data-driven failures (F01).** A step fails only on author or admin problems (bad template, credential, URL, Teams refusing it, rate limiter starved). Long or blank bound values are shortened, emptied or replaced, with a warning naming the key, never the value.
- Forbidden runtime dependencies: `AdaptiveCards.Templating` and anything from Bot Framework. Test-only dependencies are fine. No property-based testing library.
- No `#if` unless Task 3 finds a 17/18 API gap. Then `#if AUTOMATE_17` and nothing else.
- **The webhook URL is a secret.** It lives only in server config under the credential section. The connection holds `CredentialName` (non-sensitive, plain name). No settings type has an `IsSensitive` field or a URL field other than `PostMessageSettings.LinkUrl`. No code logs the URL, throws it, returns it, puts it in `Warnings`, or exports it.
- **Logs carry the status code and the byte count only** (plan review P4): never the host, never a body, never a value. Only the admin-only Test result may show the host, and only when it's a valid DNS name; run-time messages and `Warnings` never name the host. `ValidatedWebhookUrl.ToString()` never returns the host. Never pass `HttpRequestException.Message`, `RequestUri` or the original exception to `ActionResult.Failed`, and every exception we pass has a null `InnerException`.
- **Fail closed** (contract 1.4): if Automate's effective `AllowedConfigurationKeyPrefixes` covers the credential prefix, a parent, a child, or holds an empty or wildcard entry, `TryGet` refuses and every send and test fails.
- HTTP uses `Constants.HttpClients.NoRedirect`, `HttpCompletionOption.ResponseHeadersRead`, reads at most 1,024 bytes of an error body, and times out through a `CancellationTokenSource` on the injected `TimeProvider`.
- Error categories and messages are the spec's section 7.5 table, verbatim; the credential rows are the contract's texts. URL-check messages are the section 7.6 table, verbatim.
- Templates use only `TextBlock`, `FactSet`, `ColumnSet` and `Action.OpenUrl`. Link labels name the destination, never "Click here".
- No live Teams call in CI, and no webhook URL in any CI secret, test, fixture, commit, ticket or chat. Live posts only happen in Tasks 4 and 21, run by Pascal from his shell (both now in the deferred Live gate; everything else runs against the Task 4a mock).
- **Honest claims until the Live gate passes (mock-first, 2026-11-16):** only `-beta.N` versions are tagged. README, `PackageDescription` and `umbraco-marketplace.json` say plainly "not yet verified against live Teams". Nothing claims Teams support as stable until the Live gate passes. The release workflow enforces it through the `eng/live-gate-open` marker file (Task 17).
- **Repo visibility (Pascal, 2026-11-03):** public at scaffold. GitHub Free. CodeQL, gitleaks and CI run from Task 1; environment reviewers, rulesets and push protection are set up in Task 20.
- **Public docs:** `docs/` holds `design.md`, `plan.md` and `spikes.md` only (Jira's docs guard enforces it). Scope notes, review documents and council transcripts stay out of the repo. The user guide is `README.md` plus `SECURITY.md`.
- Pitfalls reference (the team's Umbraco pitfalls reference), Critical and Security items:
  - **P2 [Critical][Security]:** no static field holds request-scoped or mutable state. All services are singletons that hold only singleton-safe dependencies (`IHttpClientFactory`, `IConfiguration`, `IOptionsMonitor<>`, `ILogger<>`, `TimeProvider`, `IDomainService`, our own singletons). The rate limiter's bucket map and the site-host cache are instance fields, never statics. Test in Task 12; the store's own check is contract C16.
  - **P5 [Critical][Security]:** no Razor views. **S1 [Critical][Security] / S5 [Critical][Security]:** no controllers of any kind. **S3 [Critical][Security]:** no property editors or client assets. Reflection tests in Task 12, pack-content check in Task 17.
  - **D1 [Critical][Data integrity]** applies by analogy: saved automations store aliases, settings names and template keys. A contract snapshot test (Task 13, extended in Task 16) fails on any rename.
  - **S4 [High][Security]:** the "message received" recipe must secure Automate's webhook trigger with an authenticator (Task 18).
  - P1: everything through DI. Pure static helpers with no state are fine.
- Prose in docs, README and messages: plain UK English, except for the exact copy the spec or contract fixes.
- **Claude never runs `git commit` or `git push`.** Every commit is a checkpoint for Pascal, with no AI attribution in the message.

## Review Focus

Ordered by likelihood (plan review P12). Each line names its owning tests.

1. **A visitor submits a huge, empty or escape-heavy form value** (100 KB, 50 rows of control characters that serialise at 6 bytes each, emoji). Expected: the post still goes out within budget, with a warning naming the key; the automation never accumulates failures. Tests: Task 9 `Fit_100KbValue_SentWithinBudget`, `Fit_50RowsOf2000SixByteEscapedChars_Fits`; Task 13 `Execute_BlankBoundMessage_PostsPlaceholder`, `Execute_AdversarialData_NeverFailsValidation`; Task 16 the same; manual breaker check in Task 21.
2. **A site uses a credential name that was fine under the old rule** (`ops-alerts`, `Ops_Alerts`, 40 characters) **or a name with no value.** Expected: the contract's name or missing error, no request, no echo. Tests: contract C04 and C07 (Task 7); Task 12 `Validate_BadCredentialName_Failure_NoRequest_NoEcho`, `Validate_MissingCredential_Failure_NoRequest_NamesKeyOnly`; Task 13 `Execute_MissingCredential_FailsConfigurationError_NoRequest`. README states the rule (Task 18).
3. **A site sets `AllowedHosts` in appsettings** and expects it to *replace* the defaults (.NET's binder appends). Test: Task 6 `Options_ConfiguredAllowedHosts_ReplaceDefaults`.
4. **Several automations share one webhook and fill the limiter**, so an alert step fails with `RateLimiting`. Expected: the failure is a clean `RateLimiting`, both actions share the one bucket, Retry can recover. Tests: Task 10 `TwentyConcurrent_NoMoreThan4InAnySecond`; Task 13 `Execute_RateLimiterFull_FailsRateLimiting`; Task 16 the same plus `TwoActions_SameCredential_ShareBucket`.
5. **The webhook host leaks through a log line or a `ToString()`** (the host can carry a secret). Expected: no log holds the host. Tests: Task 6 `ValidatedWebhookUrl_ToString_HasNoHostPathOrQuery`; Task 11 `Post_LogsStatusAndBytesOnly`, `ValidatedWebhookUrl_AsStructuredLogArgument_NoHost`, `Sender_SentinelHost_NeverInRunTimeOutput`.
6. **The credential section becomes resolvable by Automate** (an Automate release widens the default allow-list, or a site adds `Umbraco:Automate` or an empty or wildcard entry). Expected: fail closed, a Critical line at startup, and CI goes red. Tests: contract C10 to C15 and I05, I06 (Task 7), run on floor and latest legs and weekly (Task 2).
7. **Retired or odd URL forms**: upper-case host, trailing dot, explicit port, `user:pass@` userinfo, IDN look-alikes. Expected: still blocked, and no message shows userinfo, path or query. Tests: Task 6 `Check_RetiredHostVariants_AreBlocked`, `Check_Messages_NeverContainUserInfoPathOrQuery`.
8. **The fitter can't converge by halving** (many long values, 64 rounds used up). Expected: values are emptied longest-first until it fits; `Validation` only when the empty-data render is over. Tests: Task 9 `Fit_64RoundsExhausted_EmptiesLongestFirst`, `Fit_AllEmptyFits_AlwaysReturnsFittingEnvelope`.
9. **The copied helper drifts from Jira's** (a local edit, a stale copy). Expected: CI fails. Tests: contract C17 and C18 (Task 7), plus the Task 7 `diff` step and the Task 19 re-copy check.
10. **Multi-byte data near the limit** (emoji, CJK, combining marks). Expected: budgets count UTF-8 bytes, cuts land on grapheme boundaries. Tests: Task 8 `Serialize_CountsUtf8Bytes`, Task 9 `Truncate_NeverSplitsGrapheme`.
11. **A data value that itself contains `{{otherKey}}`.** Expected: shown literally, never substituted a second time. Tests: Task 13 `Build_DataContainingPlaceholder_IsNotReSubstituted`, Task 14 `Render_DataContainingPlaceholder_IsNotReSubstituted`.

---

## Prerequisites (Pascal)

- [ ] **P1: M365 Developer sandbox (before Task 4).** Join the Microsoft 365 Developer Program, provision the sandbox tenant, create a team with a **private** channel `automate-test`. In the channel, create a Workflow from the "Send webhook alerts to a channel" template. Keep the URL in a password manager entry, **not** in a file in the repo. Not a client tenant and not the Manifesto tenant. **Stop rule (P13):** if the sandbox can't be provisioned before M0 starts, park Teams and decide: another tenant you control that is neither a client's nor Manifesto's, or wait (spec section 8). **Deferred (2026-11-16):** no longer a stop rule. P1 is the Live gate's prerequisite; M0 to M3 run against the mock.
- [ ] **P2: Demo sites (before Task 3).** One Umbraco **17.8.0+** site with Automate 17.5.1+ and one Umbraco **18.3.0+** site with Automate 18.5.1+, both with `Umbraco.Automate.Forms` installed (Forms runs unlicensed on localhost). For the S2 Deploy check: an Umbraco Cloud project with two environments, or a licensed local Deploy. **This is a 1.0 gate (D11):** M1 to M3 may start without it, but Task 21 can't say GO until it has run.
- [ ] **P3: Reserve the NuGet IDs** (spec section 10, F09). In a scratch folder outside the repo: `dotnet new classlib -n Umbraco.Community.Automate.MicrosoftTeams`, add a README saying "Placeholder. Not functional yet.", set `PackageReadmeFile`, then `dotnet pack -c Release -p:Version=0.0.1-alpha`. Push it with your NuGet key and **unlist** it on nuget.org. No Automate dependency, no repo link. Do the same for the look-alike `Umbraco.Community.Automate.Teams`, whose README says "Use Umbraco.Community.Automate.MicrosoftTeams."
- [ ] **P4: NuGet account safety (F09, R8).** 2FA on. Add a second owner, or write a recovery plan and keep it with the password manager entry.
- [ ] **P5: Jira inputs (D9, plan review P1 and P2).**
  - Before Task 2: Jira Task 1a is merged, so its `build-test.yml`, `dependabot.yml`, `scripts/vulnerable-check.sh`, `scripts/docs-guard.sh` and `scripts/test.sh` exist on Jira's `main` (Teams copies them).
  - Before Task 6 (M1): **Jira Task 0** (credential isolation spike S7, the gate for Jira and Teams) has a recorded row in Jira's `docs/spikes.md`, and the "S7 gate result" line at the top of this plan is filled in. **Jira Task 6a** has frozen the contract: its status line says **Frozen**, the canonical hash is set, and the Jira repo has the tag `credential-helper-1.0.0`.
- [x] **P6: Dropped (2026-11-03).** No disclosure date is needed and no report is pending. Visibility no longer depends on it.

Expected: you can post to `automate-test`, both demo sites start, and both IDs show as owned by you on nuget.org.

---

## NO-GO delta (only if Jira Task 0 records "18 fails")

Applies when Jira Task 0 step 8 records the row "18 fails (17 either way)", or the time-box row resolves to it. Row "18 passes, 17 fails" is **not** this delta: Teams goes ahead on 18 only (`eng/lines.txt` = `18`; the 17 line waits for an Automate 17.x release that passes Jira's steps 3 and 4). Row "Steps 3 and 4 pass on both, step 5 or 6 UNKNOWN" is GO, with the UNKNOWN carried by name to Task 21.

Before M1 resumes, `security-analyst` reviews this delta against the design. Then:

**Design (option 2, security review F02, with the Steward's conditions from the council transcript)**
- The connection gets `WebhookUrl` as `[Field(IsSensitive = true)]`, no bindings, instead of `CredentialName`. It accepts exactly two shapes: a full literal `https://` URL (Core encrypts it), or a whole-value `$Umbraco:Automate:Secrets:<Name>` reference.
- Anything else containing `$` followed by an allowed prefix is rejected at save with "Use either the full URL or a single `$Umbraco:Automate:Secrets:` reference." by the `ConnectionSavingNotification` handler. If Task 5 (S3) found no usable hook, the same rule runs at Test and before every send, and the README says so prominently.
- `ITeamsWebhookSender.PostAsync` takes the resolved `webhookUrl` (from the connection settings Automate has already resolved) instead of a credential name. The rate limiter keys on the first 16 hex characters of the URL's SHA-256, never the URL.
- Honest claims change to the Steward's: "never sends the URL to a host outside AllowedHosts" (only with `StrictHosts` on) and "Automate section access to a workspace that allows this connection equals holding the URL", in README, SECURITY.md and the Test result.
- The Steward's appsettings check still applies: the Test result warns when the referenced `Umbraco:Automate:Secrets:*` key is supplied by an `appsettings*.json` file.

**Tests added**
- Security review section 6 **item 5** (`TeamsConnectionSavingHandlerTests.Saving_WebhookUrlShapes`): a literal `https://` URL is accepted; a whole `$Umbraco:Automate:Secrets:X` is accepted; `https://host/...&sig=$Umbraco:Automate:Secrets:X` is rejected; `$Umbraco:Automate:Variables:H/...?sig=LIT` is rejected; every rejection's message doesn't echo the input.
- Security review section 6 **item 6** (`ArchitectureTests`): `TeamsWorkflowConnectionSettings.WebhookUrl` is `IsSensitive`; no action settings type has a URL-named property other than `LinkUrl`.
- The Steward's tests: an HTTP Request step resolving `$Umbraco:Automate:Secrets:X` reaches an arbitrary host (documents the known gap; fails on purpose when HQ fixes it); a Deploy round-trip of a `$` reference (Task 21); config reload on rotation, on both lines; no secret in logs or exports (the Task 11 leak tests, unchanged); host checks (Task 6, unchanged).

**Voided**
- Task 7 entirely (no store, no `TeamsCredentialPart`, no contract suites, no `TeamsCredentialSourceCheck`).
- Task 2's weekly schedule keeps running the suite, but there is no isolation suite to watch; keep it for Automate drift only.
- Task 12: `Validate_BadCredentialName_*`, `Validate_GuardTripped_*`, `Validate_CredentialFromAppsettingsJson_*` (replaced by the Steward's appsettings check above); the saving handler's name rule (replaced by item 5); `NoSettingsType_HasSensitiveField` (inverted by item 6); `OnlySender_CallsCredentialStoreTryGet`.
- Tasks 11, 13 and 16: every missing-credential, bad-name and guard-tripped test (the URL comes from Automate's resolver; a missing reference is Automate's own error).
- Task 18: the credential-name setup, the "never add the credential section to `AllowedConfigurationKeyPrefixes`" rule and the package-credential migration text; the Secrets-reference setup and the F-02 exposure warning replace them.
- Task 21: the credential-name refusal, the allow-list fail-closed check and the hand isolation check (step 3); replaced by the item 5 shapes on a live site and a `grep -c "sig="` of the export and the Deploy artefact (an encrypted literal must not appear in plain text).

---

## File structure

```
Directory.Build.props        Jira's property set (AutomateMajor, ranges, AutomateFloat, lock-file path, VersionPrefix), obj/bin per major, metadata
Directory.Packages.props     central versions
global.json, .gitignore, LICENSE (MIT), README.md, SECURITY.md, umbraco-marketplace.json, umbraco-marketplace-readme.md
Umbraco.Community.Automate.MicrosoftTeams.slnx
eng/lines.txt                17 and 18 (Jira's format)
scripts/                     test.sh, vulnerable-check.sh, docs-guard.sh, pack.sh, check-packed-readme.sh (copied from Jira)
.semgrep/teams.yml           (dropped: CodeQL replaces Semgrep)
.github/workflows/           build-test.yml (Task 2), pack-release.yml (Task 17), both from Jira with listed deltas
.github/dependabot.yml       Jira's file
spikes/                      M0 payloads and the post script (not packed)
SRC/
  Connection/    TeamsWorkflowConnectionType.cs, TeamsWorkflowConnectionSettings.cs, TeamsConnectionSavingHandler.cs
  Credentials/   TeamsCredentialStore.cs (verbatim copy: ICredentialStore<TPart>, store, startup check),
                 TeamsCredentialPart.cs, TeamsCredentialSourceCheck.cs
  Actions/       PostMessageAction.cs, PostMessageSettings.cs, MessageCardBuilder.cs, ConnectionChoiceValidator.cs,
                 PostCardAction.cs, PostCardSettings.cs, CardDataRow.cs, TeamsPostOutput.cs
  Cards/         CardRenderer.cs (ICardRenderer, CardRenderOptions, CardRenderResult), CardTemplates.cs,
                 CardEnvelope.cs, MarkdownEscaper.cs, ValueLimiter.cs, PayloadFitter.cs, UrlProperties.cs,
                 PersonalDataKeys.cs, Templates/{content-published,approval-needed,form-submitted,simple}.json
  Links/         SiteHostProvider.cs, LinkHostPolicy.cs
  Http/          ITeamsWebhookSender.cs, TeamsWebhookSender.cs, TeamsPostResult.cs, TeamsErrorClassifier.cs,
                 WebhookUrlValidator.cs, ValidatedWebhookUrl.cs, HostNames.cs, SecretScrubber.cs,
                 TeamsPostException.cs, WebhookRateLimiter.cs
  Configuration/ TeamsComposer.cs, TeamsOptions.cs, TeamsDocs.cs
  TeamsAssemblyMarker.cs, UmbracoCommunityAutomateTeamsSchema.cs, buildTransitive/Umbraco.Community.Automate.MicrosoftTeams.props
TESTS/
  CredentialHelperContract/  CredentialStoreContractTests.cs, CredentialIsolationContractTests.cs (verbatim from Jira),
                             TeamsCredentialStoreContractTests.cs (contract 5.3 subclasses)
  Support/       RecordingHandler.cs (fake HTTP), CountingStream.cs, CaptureLoggerProvider.cs, TelemetryCapture.cs,
                 TestUrls.cs, TestCredentials.cs, EditableModelResolverStub.cs
  Contract/      frozen-contract.json, Snapshots/*.json, adaptive-card-1.4.schema.json, adaptive-card-1.5.schema.json
  *Tests.cs      one file per production class, plus ArchitectureTests, SecretLeakTests, ContractTests
docs/            design.md, plan.md, spikes.md (nothing else; docs-guard)
```

**Why two Adaptive Card schemas (P11):** built-in templates declare version 1.4, so Task 15 validates them against the 1.4 schema: that proves they render on every client that supports 1.4. Custom cards may declare 1.5, so Task 14's URL-property walk uses the 1.5 schema, the superset, so a 1.5-only URI property can't slip past `UrlProperties`.

`TestUrls.Sentinel` is the fake webhook URL every secret test uses:
`https://default0123.ab.environment.api.powerplatform.com/powerautomate/automations/direct/workflows/SENTINELPATH/triggers/manual/paths/invoke?api-version=1&sig=SENTINELSIG`. A secret leak means any captured text contains `SENTINELPATH` or `SENTINELSIG`.

`TestCredentials.Store(string name, string url, params string[] extraAllowedPrefixes)` returns a `TeamsCredentialStore` over an in-memory `IConfiguration` holding `Umbraco:Automate:MicrosoftTeams:Credentials:{name}:WebhookUrl = url`, and a `TestOptionsMonitor<AutomateOptions>` (the contract file's) with `new AutomateOptions()` defaults plus any extra prefixes. Tests use the credential name `sentinel` for `TestUrls.Sentinel`.

Verification commands use `dotnet test -p:AutomateMajor=18 --filter <name>` for one line, and `./scripts/test.sh` (Jira's, loops `eng/lines.txt`) for every line. A pass ends with `Passed!  - Failed:     0` per line.

---

## M0: Scaffold, CI and spikes (go/no-go, time-boxed at 2 days)

M0 runs in its own session. Pascal reviews `docs/spikes.md` before Task 6 starts (portfolio "Build approach"). If the 2 days run out, any spike without an answer counts as failed for its gate, except S2 Deploy; Pascal may extend once, by one day, in writing in `docs/spikes.md`.

### Task 1: Clone and scaffold the repo

**Files:**
- Create: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.gitignore`, `LICENSE`, the `.slnx`, `SRC/*.csproj`, `SRC/TeamsAssemblyMarker.cs`, `TESTS/*.csproj`, `eng/lines.txt`, `scripts/test.sh`, `docs/spikes.md`
- Create: `docs/design.md`, `docs/plan.md` (copies)
- Test: `TESTS/ScaffoldTests.cs`

**Interfaces:**
- Produces: the `AutomateMajor` property, `AUTOMATE_17` / `AUTOMATE_18`, `AutomateFloat`, per-line lock files, `[assembly: AssemblyMetadata("AutomateMajor", "<n>")]` on the package assembly, `scripts/test.sh`, `eng/lines.txt`.

- [ ] **Step 1: Clone, copy only the two docs, start `spikes.md`**

```bash
gh repo clone PascalA94/Umbraco.Community.Automate.MicrosoftTeams
cd Umbraco.Community.Automate.MicrosoftTeams && mkdir -p docs
cp "<workspace>/03-automate-teams/design.md" "<workspace>/03-automate-teams/plan.md" docs/
```

Never copy `scope.md`, `security-review.md`, `plan-review.md` or council transcripts. Start `docs/spikes.md` with a heading per spike (S1 to S10) and an "M0 gate" table. Expected: `ls docs` shows exactly `design.md plan.md spikes.md`.

- [x] **Step 2: Visibility.** Public at scaffold (the disclosure rule was dropped on 2026-11-03). Note it in `docs/spikes.md` and do Task 20 step 1 after Task 2's first green run. Expected: the line is in `docs/spikes.md`.

- [ ] **Step 3: Write the build files**

- `global.json`: SDK `10.0.100`, `rollForward: latestFeature` (as HQ).
- `Directory.Build.props`: Jira's property set from Jira Task 1 step 2 (copy the file from the Jira repo and change only the package-specific values): `AutomateMajor` default `18`; `AutomateRange` and `UmbracoCmsRange` per line as in Global Constraints; `AutomateFloat` default `false` (when `true`, the Automate and CMS references use `$(AutomateMajor).*`); `DefineConstants` `AUTOMATE_$(AutomateMajor)`; `RestorePackagesWithLockFile true`; `NuGetLockFilePath packages.$(AutomateMajor).lock.json`; `VersionPrefix $(AutomateMajor).$(PackageMinor).$(PackagePatch)` with both defaulting to `0`. Add, Teams-specific: `BaseIntermediateOutputPath=obj/$(AutomateMajor)/` and `BaseOutputPath=bin/$(AutomateMajor)/`; `TargetFramework net10.0`, `Nullable enable`, `ImplicitUsings enable`, `GenerateDocumentationFile true`, `TreatWarningsAsErrors true`, `Deterministic`, `ContinuousIntegrationBuild` when `CI=true`, SourceLink, author/owner "Pascal Meyer", MIT, `PackageTags umbraco umbraco-marketplace automate teams microsoft`, `NuGetAudit true`, `NuGetAuditMode all`, `WarningsAsErrors` `NU1901;NU1902;NU1903;NU1904` (F08).
- `Directory.Packages.props`: central management for xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, JsonSchema.Net and Microsoft.Extensions.TimeProvider.Testing (latest stable, test-only). No FsCheck.
- `SRC/*.csproj`: `Microsoft.NET.Sdk`, references `Umbraco.Cms.Core` with `$(UmbracoCmsRange)` and `Umbraco.Automate.Core` with `$(AutomateRange)`, `AssemblyMetadata` item `AutomateMajor=$(AutomateMajor)`, `InternalsVisibleTo` the tests project.
- `TESTS/*.csproj`: references the package project and `Umbraco.Automate.Testing` at `$(AutomateRange)`.
- `eng/lines.txt` and `scripts/test.sh`: copied from the Jira repo's `main`.
- `.gitignore`: standard .NET plus `artifacts/`, `out/` and `*.local.json`.

- [ ] **Step 4: Write the failing test**

```csharp
[Fact] public void Assembly_IsBuiltForTheSelectedAutomateMajor()
{
    var major = typeof(TeamsAssemblyMarker).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "AutomateMajor").Value;
#if AUTOMATE_17
    Assert.Equal("17", major);
#else
    Assert.Equal("18", major);
#endif
}
```

- [ ] **Step 5: Run it, and check it fails.** Run: `dotnet test -p:AutomateMajor=18 --filter ScaffoldTests`. Expected: build error, `TeamsAssemblyMarker` not found.

- [ ] **Step 6: Add `internal sealed class TeamsAssemblyMarker` in `SRC/TeamsAssemblyMarker.cs`**

- [ ] **Step 7: Run on every line.** Run: `./scripts/test.sh`. Expected: two `Passed!` lines; `obj/17` and `obj/18` exist; `packages.17.lock.json` and `packages.18.lock.json` exist for both projects; `dotnet list SRC package -p:AutomateMajor=17` shows `Umbraco.Cms.Core` at `17.8.0` or later, and `-p:AutomateMajor=18` at `18.3.0` or later.

- [ ] **Checkpoint (Pascal commits):** `git add -A` / suggested message `chore: scaffold solution with the AutomateMajor 17/18 switch`

---

### Task 2: CI from the first commit (reused from Jira, plus a weekly run)

**Superseded 2026-11-03:** the Semgrep job, `.semgrep/teams.yml` and their steps below were dropped; CodeQL default setup replaces them. The shipped checks are `build (<line> <leg>)`, `secrets`, `docs-guard`, `osv / osv-scan`, `deps (<line>)` and `credential-helper`.

Plan review P6, P14 and P15. Owner for the workflow: `devops-engineer` conventions; Claude writes, Pascal pushes.

**Files:**
- Copy from the Jira repo's `main` (Jira Task 1a): `.github/workflows/build-test.yml`, `.github/dependabot.yml`, `scripts/vulnerable-check.sh`, `scripts/docs-guard.sh`
- Create: `.semgrep/teams.yml`

**Interfaces:**
- Consumes: `eng/lines.txt`, `AutomateMajor`, `AutomateFloat`, the per-line lock files (Task 1).
- Produces: checks named `build (<line> <leg>)`, `secrets`, `docs-guard`, `osv / osv-scan` and `deps (<line>)`. Every later task's tests, including Task 7's contract suites, run in them on floor and latest.

**The only Teams deltas from Jira's files** (anything else is a copy error):
1. `docs-guard.sh`: allowed files are `docs/design.md`, `docs/plan.md`, `docs/spikes.md` (no `runbook.md`); forbidden names add `scope\.md`.
2. `build-test.yml` schedule: weekly, `cron: '0 6 * * 1'`, instead of daily (private-repo Actions minutes; P15 asked for weekly).
3. `build-test.yml` new job `scheduled-failure-issue`: `needs:` every other job, `if: failure() && github.event_name == 'schedule'`, `permissions: issues: write`, runs `gh issue create --title "Scheduled CI failed: check the newest Automate release and the allow-list" --body "${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}"` with `GH_TOKEN: ${{ github.token }}`.
4. `build-test.yml` new job `semgrep`: on `pull_request` and `push`; installs a pinned Semgrep with `pip install semgrep==<exact version>`; runs `semgrep scan --config p/csharp --config p/secrets --config .semgrep/teams.yml --error --metrics=off`.

- [ ] **Step 1: Copy the four files and apply the deltas.** Then `diff` each against the Jira copy. Expected: only the four deltas above appear.

- [ ] **Step 2: Write `.semgrep/teams.yml`** with two rules, scoped to `src/`:
  - `teams-no-httprequestexception-message`: pattern `(HttpRequestException $E).Message`, message "Never read HttpRequestException.Message: it can hold the webhook URL (spec 7.5)."
  - `teams-no-requesturi-in-logs`: pattern-either `$LOG.LogInformation(..., $X.RequestUri, ...)`, `$LOG.LogWarning(..., $X.RequestUri, ...)`, `$LOG.LogError(..., $X.RequestUri, ...)`, `$LOG.LogDebug(..., $X.RequestUri, ...)`, `$LOG.LogTrace(..., $X.RequestUri, ...)`, message "Never log RequestUri (spec 7.5)."
  Run locally: `semgrep scan --config .semgrep/teams.yml --error`. Expected: exit 0 on the scaffold.

- [ ] **Step 3: Lint.** Run: `actionlint .github/workflows/build-test.yml`. Expected: no output. Check by eye: no `pull_request_target`; every `uses:` pinned to a 40-character SHA; top-level `permissions: {}` or `contents: read` as in Jira's file; `issues: write` only on the new job.

- [ ] **Step 4: Prove it runs (Pascal pushes).** Push a branch and open a draft PR. Expected: one green `build-test` job per line and leg, green `gitleaks`, `docs-guard` and `semgrep`. Record the run URL and each leg's resolved Automate version in `docs/spikes.md`.

- [ ] **Step 5: Prove Semgrep blocks (once; gitleaks and the vulnerable check were proven in Jira).** On a throwaway branch, add `_ = ((HttpRequestException)new HttpRequestException()).Message;` to a source file and open a PR. Expected: `semgrep` fails naming `teams-no-httprequestexception-message`. Close the PR unmerged and delete the branch.

- [ ] **Checkpoint (Pascal commits):** `git add .github scripts .semgrep docs/spikes.md` / suggested message `ci: build and test both lines on floor and latest Automate, with secret, CVE, docs and Semgrep checks`

Until Task 20 step 1 adds the branch ruleset, merge only on green.

---

### Task 3: Gate A, the one-branch build (spikes S7 and S6)

The go/no-go for D7. Builds the real connection and Post message skeletons on both majors and installs them into both demo sites.

**Files:**
- Create: `SRC/Connection/TeamsWorkflowConnectionSettings.cs`, `SRC/Connection/TeamsWorkflowConnectionType.cs`, `SRC/Actions/PostMessageSettings.cs`, `SRC/Actions/PostMessageAction.cs`, `SRC/Actions/TeamsPostOutput.cs`, `SRC/Actions/CardDataRow.cs`, `TESTS/Support/EditableModelResolverStub.cs`
- Test: `TESTS/SkeletonTests.cs`
- Modify: `docs/spikes.md`

**Interfaces:**
- Produces:
  - `TeamsWorkflowConnectionSettings { string? CredentialName; string DefaultCardVersion = "1.4"; }`. `CredentialName` is a plain, **non-sensitive** `[Field]` with no bindings. There is no `WebhookUrl` property (spec 6.1, D9). `DefaultCardVersion` uses a dropdown of `1.2`/`1.3`/`1.4`/`1.5` in `Group = "Advanced"`.
  - `TeamsWorkflowConnectionType : ConnectionTypeBase<TeamsWorkflowConnectionSettings>`, attributes as in Global Constraints. For now `ValidateAsync` returns the base warning.
  - `PostMessageAction : ActionBase<PostMessageSettings, TeamsPostOutput>` with `[Action("community.teams.postMessage", "Post Teams message", Group = "Messaging", Icon = "icon-message", ConnectionTypeAlias = "community.teams.workflowWebhook")]`. For now it reads `context.Connection!.GetSettings<TeamsWorkflowConnectionSettings>()` and returns `Success(new TeamsPostOutput())`.
  - `PostMessageSettings { string? Title; string? Message; List<CardDataRow> Data = []; string? LinkUrl; string? LinkLabel; MessageStyle Style; MessageFormatting Formatting = PlainText; bool AllowExternalLinks; }`. Bindings are on for the four strings and the `Data` row values. Enums: `MessageStyle { Default, Good, Warning, Attention }`, `MessageFormatting { PlainText, Markdown }` (Plain first and the default, F03). `AllowExternalLinks` sits in `Group = "Advanced"`.
  - `CardDataRow { string Key; string? Value; }` (default; Step 6 may switch to HQ's type).
  - `TeamsPostOutput { int StatusCode; bool Accepted; int PayloadBytes; string[] Warnings = []; }`
  - `EditableModelResolverStub : IEditableModelResolver` (test support; S6).

- [ ] **Step 1: Write the failing test**

```csharp
[Fact] public async Task PostMessage_Skeleton_ReadsConnectionSettings_AndSucceeds()
{
    var result = await ActionTestHarness.For<PostMessageAction>()
        .WithService<IEditableModelResolver>(new EditableModelResolverStub())   // explicit, per S6
        .WithSettings(new PostMessageSettings { Message = "hi" })
        .WithConnection(new TeamsWorkflowConnectionSettings { CredentialName = "sentinel" })
        .ExecuteAsync();
    Assert.True(result.IsSuccess);
}
```

(Use the real member names for the success flag and the resolver that Step 3 records.)

- [ ] **Step 2: Run on every line, and check it fails.** Run: `./scripts/test.sh`. Expected: build errors for the missing types.

- [ ] **Step 3: Implement the skeletons, then compare 17 with 18**

Check these on both majors: `ConnectionTypeBase<T>` constructor and `ValidateAsync`; `ConnectionValidationResult.Success/Warning/Failure`; `ActionContext.GetSettings<T>()` and `Connection.GetSettings<T>()`; `ActionResult.Failed(Exception, StepRunErrorCategory)`; `IValidatableStepType.ValidateSettingsAsync` and `IPublishValidatableStepType.ValidateSettingsForPublishAsync` (both return `IReadOnlyList<string>`, and every string is an error); **whether `ValidateSettingsForPublishAsync` can read the step's `ConnectionId` and the workspace's allowed connections, and through which public service (F06)**; `[Field(VisibleWhen, VisibleWhenValues)]`; the CLR type `UmbracoAutomate.PropertyEditorUi.KeyValueEditor` binds to (HQ uses `List<HttpRequestKeyValue>` with an internal converter); `AutomateOptions.AllowedConfigurationKeyPrefixes` is public and bound to `Umbraco:Automate`; `UmbracoApplicationStartedNotification` exists on both lines (the contract's startup check needs it); how `ActionTestHarness` registers `IEditableModelResolver` and how a test captures the step's context logger (S6, needed by Tasks 13 and 16); and the state of PRs #445 and #449.

- [ ] **Step 4: Run on every line.** Run: `./scripts/test.sh`. Expected: two `Passed!` lines.

- [ ] **Step 5: Install into both demo sites (Pascal).** `dotnet pack SRC -c Release -p:AutomateMajor=17 -p:VersionSuffix=spike.1 -o artifacts`, then the same for 18. Add `artifacts/` as a local NuGet source on each demo site, install, and start the site. Check that the connection type and the action appear in the backoffice pickers, and that the connection shows a plain credential-name field and no masked field.

- [ ] **Step 6: Record the gate in `docs/spikes.md`.** Each item from Step 3 as "same" or "differs", the `CardDataRow` decision, the F06 publish-validation route (or "not possible": then spec 8's fallback applies, Pascal is told, and Task 13 adds the conditional fallback test), the harness setup and the context-logger route.
**GO** if every member is the same, or the gaps fit in `#if AUTOMATE_17` blocks in at most 3 files. **NO-GO (structural, plan review P13)** if more than 3 files need `#if`, the lines need different TFMs one test project can't cover, or DI registration, service lifetimes or the composer API differ: stop and ask Pascal before splitting branches.

- [ ] **Checkpoint (Pascal commits):** `git add SRC TESTS docs/spikes.md` / suggested message `feat: connection and post message skeletons build on Automate 17 and 18`

---

### Task 4: Gate B, live Teams behaviour (spikes S1 and S5)

**Mock-first (2026-11-16):** Steps 1 and 2 run in M0 (the files are needed anyway). Steps 3 and 4 and this gate are **DEFERRED** to the Live gate. Record S1 and S5 in `docs/spikes.md` as "DEFERRED (mock-first)" with the documented assumptions from Task 4a. A deferred Task 4 doesn't count as failed for the M0 time box.

Pascal runs every post. Claude prepares the files and never sees the URL.

**Files:**
- Create: `spikes/post.sh`, `spikes/payloads/*.json`, the four draft templates in `SRC/Cards/Templates/*.json` (version `1.4`, `{{key}}` placeholders, keys from spec 6.3)
- Modify: `docs/spikes.md`

- [ ] **Step 1: Write `spikes/post.sh <payload.json>`.** It reads the URL from `$TEAMS_SPIKE_URL`, POSTs with `curl -sS -o body.tmp -w '%{http_code}'`, and prints only the status code, the body length, the first 200 body characters (passed through `sed -E 's/sig=[^&" ]+/sig=[redacted]/g; s#https?://[^ "]+#[redacted]#g'`) and the URL host (`awk -F/ '{print $3}'`, so Pascal can set `AllowedHosts`). It must never echo the URL or run with `set -x`.

- [ ] **Step 2: Write the payloads.** `bare-text.json` (`{"text":"..."}`). The four templates, filled with sample data, at each of versions 1.2, 1.3, 1.4 and 1.5, with and without `"msteams":{"width":"Full"}`. `summary-vs-fallback.json` with different `summary` and `fallbackText` values. `escaped-data.json` containing `Rock \"n\" Roll - 1.0`, `\[x\]\(https://e.example\)` and `\*a\*`, plus a zero-width-space variant. `autolink.json` (F03) with bare `https://e.example/login`, `www.e.example`, `user@e.example` and `mailto:user@e.example` in a `TextBlock` and a `FactSet`, each plain and with U+200B after `://`, after `www.` and before `@`, plus a U+2060 variant.

- [ ] **Step 3: Pascal posts**

```bash
read -rs TEAMS_SPIKE_URL && export TEAMS_SPIKE_URL
for f in spikes/payloads/*.json; do echo "$f"; ./spikes/post.sh "$f"; done
```

Also post one card from the v18 demo site's built-in HTTP Request action, to see the run-log body. Check desktop, web and mobile.

- [ ] **Step 4: Record in `docs/spikes.md`.** Status and body (expected `202`, empty), whether bare text fails, the host **pattern** (for example `*.environment.api.powerplatform.com`, never the tenant-specific host, because `spikes.md` is public), which card versions render on all three clients, whether `summary` or `fallbackText` drives the notification preview, `width: Full` yes or no, and whether Teams honours backslash escapes (if not, the escaping method is the zero-width fallback from spec section 8). Per client, whether each `autolink.json` variant is clickable. The U+200B link breaking ships either way; if it doesn't stop a link on some client, use the next fallback in spec section 8 and tell `security-analyst`.
**GO** if the sandbox accepted posts and at least one card version renders everywhere. **NO-GO** if there is no tenant, or no version renders: do not start M1 (spec section 8 stop rule). *(Superseded 2026-11-16: with no tenant this gate is DEFERRED, not NO-GO, and M1 starts on the mock. At the Live gate, "no version renders" is still a NO-GO for any stable release.)*

- [ ] **Checkpoint (Pascal commits):** `git add spikes SRC/Cards/Templates docs/spikes.md` / suggested message `docs: record Teams Workflows spike findings`. Before you commit, check `git diff --cached` contains no `sig=` and no tenant-specific host.

---

### Task 4a: Teams Workflows mock and the one facts file (mock-first, 2026-11-16)

Replaces Task 4's live posts until the Live gate. Pattern: Jira's `FakeJiraHandler` through `TestHost.Compose` and Deploy Hooks' test handler, which replace only the named client's primary handler.

**Files:**
- Create: `SRC/Configuration/TeamsWorkflowsFacts.cs`, `tests/Umbraco.Community.Automate.MicrosoftTeams.Mock/MockTeamsWorkflowsHandler.cs` (a small non-packed library), `eng/live-gate-open`
- Modify: the demo site (Development only: mock switch), `docs/spikes.md`
- Test: `TESTS/MockTeamsWorkflowsHandlerTests.cs`

**Interfaces:**
- Produces:
  - `internal static class TeamsWorkflowsFacts`: **the only place** that encodes Teams behaviour. Accepted status `202` with an empty body; expected envelope (`type: message`, one or more `attachments` with `contentType: application/vnd.microsoft.card.adaptive`); default card version `1.4` and supported versions `1.2` to `1.5`; default host patterns (today's `AllowedHosts` defaults); size limit `28000` bytes; throttling (status `429`, `Retry-After`); error shapes for bad JSON, oversized body and a deleted or turned-off workflow. Every member has a why-comment `[unverified: from Microsoft docs, not tested live]` and the doc URL.
  - `TeamsOptions` gains `string DefaultCardVersion` and `string[] CardVersions`, and `AllowedHosts`, `MaxPayloadBytes` and `RateLimit` take their defaults from `TeamsWorkflowsFacts`. Sites can override every one in config. No other file holds these values as constants.
  - `MockTeamsWorkflowsHandler : HttpMessageHandler`: POST only; `application/json` only; parses the body and checks the envelope; returns `202` with an empty body when valid; rejects bad JSON and bodies over the facts' size limit with the facts' error shape; a test-set mode returns `429`, `404` (workflow deleted), `500` or never completes. Records each accepted envelope for assertions. All values come from `TeamsWorkflowsFacts` (`InternalsVisibleTo` the mock library).
- **Why a primary-handler swap, not a socket listener:** `NoRedirect`'s primary handler is `SsrfProtectionHandler`, which blocks `127.0.0.1`. Swapping only the primary handler keeps the real URL checks, limiter, size check, sender and classifier in play.

- [ ] **Step 1: Write the failing tests:** `Mock_ValidEnvelope_Returns202EmptyBody`, `Mock_BadJson_RejectedWithFactsShape`, `Mock_OverSizeLimit_RejectedWithFactsShape`, `Mock_NotPost_Or_NotJson_Rejected`, `Mock_ModeThrottled_Returns429WithRetryAfter`, `TeamsOptions_DefaultsComeFromFacts` (a bound-from-empty-config `TeamsOptions` equals the `TeamsWorkflowsFacts` values).
- [ ] **Step 2: Run, check they fail, implement, run on every line.** Expected: `Failed: 0` per line.
- [ ] **Step 3: Demo site mock (Development only).** A demo-only switch (`TeamsMock:Enabled`, default false, never in the package) swaps the `community.teams` client's primary handler for the mock. Accepted envelopes go to a git-ignored `demo/.teams-mock/` folder so Pascal can paste them into the Adaptive Cards Designer (Teams host). The demo credential is an env var like any real one, set to a made-up URL on a `.invalid` host. No real or real-looking Workflows URL in any file.
- [ ] **Step 4: Record in `docs/spikes.md`:** S1 and S5 "DEFERRED (mock-first)", plus a facts table (one row per `TeamsWorkflowsFacts` member, its value, its doc URL), each tagged **[unverified: from Microsoft docs, not tested live]**. Create `eng/live-gate-open` with one line: "Live Teams gate not yet passed. See docs/plan.md, Live gate."
- [ ] **Checkpoint (Pascal commits):** `git add SRC/Configuration tests eng demo docs/spikes.md` / suggested message `test: Teams Workflows mock and documented facts (live gate deferred)`. Before you commit, check `git diff --cached` contains no `sig=` and no real host.

---

### Task 5: Gate C, Umbraco-side behaviour and the Jira gate (S2, S3, S4, S8, S9, S10)

**Files:**
- Modify: `docs/spikes.md`

- [ ] **Step 1: S2, the Jira gate (D9, plan review P2).** Read Jira's `docs/spikes.md` Task 0 row and the "S7 gate result" line at the top of this plan. Copy into Teams' `docs/spikes.md`: the row and date; Jira's rotation record (Task 0 step 5, per provider); Jira's surfaces record (Task 0 step 7, council test 10). Copy no secrets and no tenant-specific hosts. Then apply:
  - "Both lines pass" or "Steps 3 and 4 pass on both, step 5 or 6 UNKNOWN": GO for M1 once the contract is Frozen (P5). Carry any UNKNOWN by name to Task 21.
  - "18 passes, 17 fails": GO on 18 only. Set `eng/lines.txt` to `18`.
  - "18 fails": apply the NO-GO delta section after `security-analyst` reviews it.
  - PENDING: M0 can finish, M1 waits.

- [ ] **Step 2: S2, Deploy (F14, D11, council tests 4 and 5)**
  - In the skeleton connection, enter credential name `spike`. Export an automation that uses it and grep the export for the URL and `sig=`.
  - **Deploy-transfer the connection** (not only the automation) to a second environment that has its own `spike` credential. Grep the transfer artefact and the target database for either URL and `sig=`. Run the skeleton action on the target with a temporary line that logs the first 8 hex characters of the value's SHA-256 (never the value, never the host; removed before the commit), to confirm it resolved the target's own value.
  - If no Deploy environment is available yet, record "Deploy: UNKNOWN". This is the **only** spike allowed to stay UNKNOWN (plan review P13); it blocks GO in Task 21, not M1.

- [ ] **Step 3: S3, connection save and validation.** Can a `ConnectionSavingNotification` handler read the raw `CredentialName` and cancel the save with a message shown in the backoffice? How does the backoffice show `ConnectionValidationResult.Warning` from Test connection: as a pass with a warning, or as a failure? Check on both majors.

- [ ] **Step 4: S4, framework logging and telemetry (F07, F10).** On the v18 site, with Serilog at `Debug` for `System.Net.Http`, **Application Insights (classic SDK) and OpenTelemetry HttpClient instrumentation with a file or console exporter switched on**, POST to a non-routable https URL carrying a dummy `?sig=SPIKESIG` through `Constants.HttpClients.NoRedirect`. Run `grep -r SPIKESIG umbraco/Logs` and grep the exported telemetry. Confirm `NoRedirect`'s primary handler is `SsrfProtectionHandler`. Also POST to `https://127.0.0.1/` and record the exception type and message that `SsrfProtectionHandler` throws, and how a test can stub the primary handler.

- [ ] **Step 5: S8, run-log warnings.** Is there an API that lets a successful step show a warning in the run log?

- [ ] **Step 6: S9, bindings.** Record the binding expressions for content name, public URL, backoffice edit URL and editor name, plus the Forms "entry submitted" trigger alias, form name and entry ID.

- [ ] **Step 7: S10, circuit breaker (F01).** Read `CircuitBreakerService` on both lines. Record: does a step that fails then succeeds on Retry count as a failed run? Do runs skipped by Core's rate limit count?

- [ ] **Step 8: Record the decisions and the M0 gate in `docs/spikes.md`.** Default decisions if a spike goes badly (spec section 8):
  - S2 Deploy carries a URL → High, back to Pascal before 1.0.
  - S2 (Jira's record) rotation needs a restart for a provider → the rotation runbook says so for that provider.
  - S2 (Jira's record) a config reader shows the credential section → residual risk; tell `security-analyst`.
  - S3: no usable save hook → the name rule runs at Test and before every send only, and the README says so. `Warning` shows as a pass → the unknown-host result uses `Warning`; otherwise `Success` with the warning appended.
  - S4: `sig=` appears in logs or telemetry → Task 11 registers a named client `community.teams` with `RemoveAllLoggers()`, no redirects and the SSRF primary handler; the telemetry docs carry the processor snippet either way.
  - S8: no API → `Warnings` output plus an `ILogger` warning.
  - S9: Forms can't be tested → the docs example is marked "not yet tested against Forms".
  - S10: Retry successes don't count → note it in the throttling docs.
  Then update this plan's Global Constraints and Tasks 6, 11 and 12 wherever a decision changes them. **GO to M1** when every spike from S1 to S10 has an answer (S2 Deploy may be UNKNOWN; S1 and S5 may be DEFERRED under mock-first, with Task 4a done), Step 1 says GO, and the contract is Frozen. Pascal reviews `docs/spikes.md` before Task 6.

- [ ] **Checkpoint (Pascal commits):** `git add docs/spikes.md` / suggested message `docs: record Automate-side spike findings and the M0 gate`. Before you commit, check `git diff --cached` contains no `sig=` and no spike URL.

---

## M1: Connection, Post message, error mapping and secret tests

Starts only when Task 5 step 8 says GO (Jira Task 0 GO, contract Frozen).

### Task 6: Options and URL checks

**Re-verify at the live gate (mock-first, 2026-11-16):** `AllowedHosts` defaults (S1 host pattern) and the card version defaults.

**Files:**
- Create: `SRC/Configuration/TeamsOptions.cs`, `SRC/Configuration/TeamsDocs.cs`, `SRC/Configuration/TeamsComposer.cs`, `SRC/Http/WebhookUrlValidator.cs`, `SRC/Http/ValidatedWebhookUrl.cs`, `SRC/Http/HostNames.cs`
- Test: `TESTS/WebhookUrlValidatorTests.cs`, `TESTS/TeamsOptionsTests.cs`

**Interfaces:**
- Produces:
  - `TeamsOptions { const string SectionName = "Umbraco:Automate:MicrosoftTeams"; string[] AllowedHosts = []; bool StrictHosts; string[] AllowedLinkHosts = []; int MaxPayloadBytes = 28000; int MaxValueLength = 2000; int TimeoutSeconds = 15; RateLimitOptions RateLimit = new(); static IReadOnlyList<string> DefaultAllowedHosts; }`. No `Credentials` property; the credential prefix is `TeamsCredentialStore.Prefix` (Task 7).
  - `RateLimitOptions { double RequestsPerSecond = 3; int Burst = 4; int MaxWaitSeconds = 5; }`
  - `TeamsOptionsPostConfigure : IPostConfigureOptions<TeamsOptions>`: if `AllowedHosts` is empty, it becomes `DefaultAllowedHosts`. Clamps `MaxWaitSeconds` to `TimeoutSeconds`, and `MaxValueLength`, `Burst` and `RequestsPerSecond` to at least 1.
  - `TeamsDocs.SetupUrl` = `https://github.com/PascalA94/Umbraco.Community.Automate.MicrosoftTeams#setup`. `TeamsDocs.SecurityUrl` = the repo's `SECURITY.md`.
  - `static string HostNames.Normalise(string host)`: lower case, one trailing dot stripped, `IdnMapping` to the `xn--` form. `static bool HostNames.IsDnsName(string host)`: letters, digits, `-` and `.`, at most 253 chars. `static bool HostNames.Matches(string host, IEnumerable<string> patterns)`: the wildcard rule. Shared with `LinkHostPolicy` (Task 9).
  - `ValidatedWebhookUrl`: an internal constructor, `internal Uri Uri`, `internal string Host`, and `ToString()` returns the constant `"[webhook URL]"` (plan review P4).
  - `WebhookUrlCheck(ValidatedWebhookUrl? Url, string? Error, string? Warning, string? TestWarning)` with `bool IsBlocked => Url is null`. `Warning` (run time) never names the host; `TestWarning` (Test result only) names it when `IsDnsName`.
  - `WebhookUrlValidator(IOptionsMonitor<TeamsOptions>)` with `WebhookUrlCheck Check(string? url)`.
  - `TeamsComposer : IComposer`, created here: binds `TeamsOptions` to `SectionName`, registers the post-configure and `WebhookUrlValidator` (singleton). Later tasks add their own registrations here (P11).

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData(null, "Webhook URL is required.")]
[InlineData("  ", "Webhook URL is required.")]
[InlineData("not a url", "This isn't a valid URL.")]
[InlineData("/relative/path", "This isn't a valid URL.")]
[InlineData("http://x.environment.api.powerplatform.com/a?sig=S", "Teams webhook URLs must use https.")]
public void Check_BlockRows(string? url, string error) { var c = Sut().Check(url); Assert.True(c.IsBlocked); Assert.Equal(error, c.Error); }

[Theory]
[InlineData("https://acme.webhook.office.com/webhookb2/x?sig=S")]
[InlineData("https://ACME.WEBHOOK.OFFICE.COM/x")]
[InlineData("https://acme.webhook.office.com./x")]
[InlineData("https://acme.webhook.office.com:443/x")]
[InlineData("https://user:pass@acme.webhook.office.com/x")]
public void Check_RetiredHostVariants_AreBlocked(string url)
{ var c = Sut().Check(url); Assert.True(c.IsBlocked);
  Assert.StartsWith("This is a retired Office 365 connector URL. Create a Workflows webhook instead.", c.Error);
  Assert.Contains(TeamsDocs.SetupUrl, c.Error); }

[Fact] public void Check_UnknownHost_WarnsButAllows_RunTimeWarningHasNoHost()
{ var c = Sut().Check("https://hooks.example.org/p?sig=S"); Assert.False(c.IsBlocked);
  Assert.Equal("The webhook host isn't a known Power Automate host.", c.Warning);
  Assert.Equal("hooks.example.org isn't a known Power Automate host.", c.TestWarning); }

[Fact] public void Check_UnknownHost_StrictHosts_Blocks()           // "The webhook host isn't in AllowedHosts."
[Fact] public void Check_SentinelBuiltHost_NeverEchoed()            // https://SENTINELSIG.example.org/: Warning has no "SENTINEL"; TestWarning only if IsDnsName
[Fact] public void Check_NonDnsHost_TestWarningUsesGenericText()    // a host with "_" or over 253 chars
[Theory] [InlineData("https://powerplatform.com/x")]                // apex is not covered by *.powerplatform.com
public void Check_WildcardDoesNotMatchApex(string url) => Assert.NotNull(Sut().Check(url).Warning);
[Theory]
[InlineData("https://xn--acme-9oa.webhook.office.com/x")]          // IDN (punycode) form of a retired host
[InlineData("https://acme.webhoοk.office.com/x")]                   // Greek omicron look-alike: compared as xn--, warned as unknown
public void Check_IdnHosts_NormalisedBeforeMatching(string url)
[Fact] public void Check_KnownHost_NoWarning() => Assert.Null(Sut().Check(TestUrls.Sentinel).Warning);
[Fact] public void Check_Messages_NeverContainUserInfoPathOrQuery() // every row; assert no "pass", "SENTINEL", "sig="
[Fact] public void ValidatedWebhookUrl_ToString_HasNoHostPathOrQuery()   // equals "[webhook URL]"; no "powerplatform", no "SENTINEL"
[Fact] public void Options_ConfiguredAllowedHosts_ReplaceDefaults()  // bind ["a.example"] -> exactly ["a.example"]
[Fact] public void Options_NoConfig_UsesDefaults()                   // including StrictHosts false, MaxValueLength 2000, RateLimit 3/4/5
[Fact] public void Options_MaxWaitClampedToTimeout()
[Fact] public void Options_BindingNeverReadsCredentials()            // config with Credentials:x:WebhookUrl binds; nothing on TeamsOptions holds the URL
```

- [ ] **Step 2: Run, and check they fail.** Run: `dotnet test -p:AutomateMajor=18 --filter "WebhookUrlValidatorTests|TeamsOptionsTests"`. Expected: build errors for the missing types.

- [ ] **Step 3: Implement.** Follow the spec 7.6 order. `*.x.com` matches a host that ends with `.x.com` and is longer than it; matching is case-insensitive. Read the options per call through `CurrentValue`.

- [ ] **Step 4: Run on every line, and check they pass.** Run: `./scripts/test.sh`. Expected: `Passed!` per line.

- [ ] **Checkpoint (Pascal commits):** `git add SRC/Configuration SRC/Http TESTS` / suggested message `feat: webhook URL checks and Teams options`

---

### Task 7: Package credentials, copied verbatim under the contract (D9)

Plan review P1, P3. Follows the contract's copy checklist (section 4) exactly. Only the class name, prefix, namespace and parts enum are Teams-specific. Security-relevant, but the build approach is native: `security-analyst` sees it in Task 19.

**Files:**
- Create (copied, four tokens replaced): `SRC/Credentials/TeamsCredentialStore.cs`
- Create: `SRC/Credentials/TeamsCredentialPart.cs`, `SRC/Credentials/TeamsCredentialSourceCheck.cs`, `TESTS/Support/TestCredentials.cs`
- Create (copied verbatim): `TESTS/CredentialHelperContract/CredentialStoreContractTests.cs`, `TESTS/CredentialHelperContract/CredentialIsolationContractTests.cs`
- Create: `TESTS/CredentialHelperContract/TeamsCredentialStoreContractTests.cs` (contract 5.3), `TESTS/TeamsCredentialNameTests.cs`, `TESTS/TeamsCredentialSourceCheckTests.cs`
- Modify: `SRC/Configuration/TeamsComposer.cs`, the test csproj (embedded store source)

**Interfaces:**
- Consumes: Jira tag `credential-helper-1.0.0`; Jira Task 0 step 7's resolver route (inside the verbatim isolation file).
- Produces:
  - `enum TeamsCredentialPart { WebhookUrl }`
  - `internal sealed class TeamsCredentialStore(IConfiguration configuration, IOptionsMonitor<AutomateOptions> automateOptions) : ICredentialStore<TeamsCredentialPart>` with `public const string Prefix = "Umbraco:Automate:MicrosoftTeams:Credentials"` and, exactly as contract 1.1: `bool TryNormaliseName(string? name, out string? normalised, out string? error)`, `bool MatchesNameRule(string candidate)`, `bool TryGetExposingPrefix(out string? entry)`, `bool TryGet(string? name, TeamsCredentialPart part, out string? value, out string? error)`, `string KeyFor(string normalisedName, TeamsCredentialPart part)`. Behaviour and error texts exactly as contract 1.2 to 1.6.
  - `TeamsCredentialStoreStartupCheck : INotificationHandler<UmbracoApplicationStartedNotification>` (contract 1.5).
  - `TeamsCredentialSourceCheck(IConfiguration configuration, TeamsCredentialStore store)` with `string? AppsettingsFileSupplying(string normalisedName)`: walks `((IConfigurationRoot)configuration).Providers` in reverse for `store.KeyFor(normalisedName, TeamsCredentialPart.WebhookUrl)` using `TryGet(key, out _)`, and returns the file name when the first provider that has it is a `JsonConfigurationProvider` whose `Source.Path` matches `appsettings*.json`; otherwise null. Never returns, stores or logs the value (council condition 7; mirrors Jira's `JiraCredentialSourceCheck`).
  - `TestCredentials.Store(...)` as described under File structure.

- [ ] **Step 1: Confirm the contract is frozen.** Read `<workspace>/credential-helper-contract.md`. Expected: status **Frozen**, a 64-hex canonical hash, version `1.0.0`. Then `git -C <path to a Jira clone> fetch --tags && git -C <path to a Jira clone> rev-parse credential-helper-1.0.0`. Expected: a commit hash. If either fails, stop: M1 isn't open (Global Constraints, Gate).

- [ ] **Step 2: Copy the two suites verbatim**

```bash
J=<path to a Jira clone>; T=tests/Umbraco.Community.Automate.MicrosoftTeams.Tests.Unit/CredentialHelperContract
mkdir -p "$T"
for f in CredentialStoreContractTests.cs CredentialIsolationContractTests.cs; do
  git -C "$J" show "credential-helper-1.0.0:tests/Umbraco.Community.Automate.Jira.Tests/CredentialHelperContract/$f" > "$T/$f"
  git -C "$J" show "credential-helper-1.0.0:tests/Umbraco.Community.Automate.Jira.Tests/CredentialHelperContract/$f" | diff - "$T/$f"
done
```

Expected: no `diff` output. Both files start with `// credential-helper-contract: 1.0.0`. If the verbatim isolation file uses `#if AUTOMATE_17`, Task 1's define makes it compile unchanged.

- [ ] **Step 3: Write the subclass file** `TeamsCredentialStoreContractTests.cs` exactly as contract 5.3, with `Prefix => TeamsCredentialStore.Prefix`, `Namespace => "Umbraco.Community.Automate.MicrosoftTeams.Credentials"`, `StoreClassName => "TeamsCredentialStore"`, `PartEnumName => "TeamsCredentialPart"`, `SamplePart => TeamsCredentialPart.WebhookUrl`, and one-line `CreateStore`, `CreateStartupCheck`, `RunStartupCheck`. Add to the test csproj: `<EmbeddedResource Include="..\..\src\Umbraco.Community.Automate.MicrosoftTeams\Credentials\TeamsCredentialStore.cs" LogicalName="CredentialHelperContract.StoreSource.cs" />`.

- [ ] **Step 4: Write the Teams-only failing tests**

```csharp
// TeamsCredentialNameTests: names the contract's C04 list doesn't cover, but a Teams admin is likely to paste
[Theory]
[InlineData("https://default0123.ab.environment.api.powerplatform.com/x?sig=SENTINELSIG")]
[InlineData("$Umbraco:Automate:Secrets:X")]
[InlineData("ops-alerts")] [InlineData("Ops_Alerts")]        // valid before the contract, rejected now
public void TryNormaliseName_PastedOrLegacyNames_RejectedWithoutEcho(string name)
// false; error == "Credential name must be 1 to 32 letters or digits."; error contains no part of the input

[Fact] public void KeyFor_UsesTeamsPrefixAndPartName()
// KeyFor("alerts", WebhookUrl) == "Umbraco:Automate:MicrosoftTeams:Credentials:alerts:WebhookUrl"

// TeamsCredentialSourceCheckTests
[Fact] public void AppsettingsJson_ReturnsFileName_NotValue()        // JSON provider for appsettings.Development.json supplies the key: returns that file name; no "SENTINEL" in it
[Fact] public void EnvironmentVariables_ReturnsNull()                // env-var provider supplies it (added last): null
[Fact] public void UserSecretsJson_ReturnsNull()                     // secrets.json supplies it: null
[Fact] public void LaterProviderWins_ReturnsNullWhenEnvOverridesJson() // JSON then env var: null
[Fact] public void KeyAbsent_ReturnsNull()
```

- [ ] **Step 5: Run, and check they fail.** Run: `dotnet test -p:AutomateMajor=18 --filter "FullyQualifiedName~CredentialHelperContract|TeamsCredentialNameTests|TeamsCredentialSourceCheckTests"`. Expected: compile failure (store missing).

- [ ] **Step 6: Copy the store and replace exactly the four tokens**

```bash
J=<path to a Jira clone>; S=src/Umbraco.Community.Automate.MicrosoftTeams/Credentials
mkdir -p "$S"
git -C "$J" show credential-helper-1.0.0:src/Umbraco.Community.Automate.Jira/Credentials/JiraCredentialStore.cs \
| sed -e 's/Umbraco:Automate:Jira:Credentials/Umbraco:Automate:MicrosoftTeams:Credentials/g' \
      -e 's/Umbraco\.Community\.Automate\.Jira\.Credentials/Umbraco.Community.Automate.MicrosoftTeams.Credentials/g' \
      -e 's/JiraCredentialStore/TeamsCredentialStore/g' \
      -e 's/JiraCredentialPart/TeamsCredentialPart/g' > "$S/TeamsCredentialStore.cs"
```

Order matters as contract section 3 step 3 lists it; `JiraCredentialStoreStartupCheck` becomes `TeamsCredentialStoreStartupCheck` through the third rule. Write `TeamsCredentialPart.cs` with the one member. Write `TeamsCredentialSourceCheck`. In `TeamsComposer`, add exactly the contract 1.1 lines plus the source check:

```csharp
builder.Services.AddSingleton<TeamsCredentialStore>();
builder.AddNotificationHandler<UmbracoApplicationStartedNotification, TeamsCredentialStoreStartupCheck>();
builder.Services.AddSingleton<TeamsCredentialSourceCheck>();
```

- [ ] **Step 7: Run on every line.** Run: `./scripts/test.sh`. Expected: `Failed: 0` per line, with **every** C01 to C18 and I01 to I06 test passing, C17 (hash) and C18 (version) included. The isolation controls I04 (`$Umbraco:Automate:Secrets` value leaks) and I05 (widening the allow-list leaks the package value and the store refuses) must **pass**: that proves the harness can see a leak. If either fails, stop and raise it with Jira's owner; never edit a copied file or an assertion.

- [ ] **Step 8: Mutation check of the controls (then revert).** Temporarily add `Umbraco:Automate:MicrosoftTeams` to the default allow-list inside `AutomateResolverHost` and run `dotnet test -p:AutomateMajor=18 --filter "FullyQualifiedName~CredentialIsolation"`. Expected: I03 FAILs (leak detected). Revert with `git checkout -- TESTS/CredentialHelperContract`. Temporarily change the store's regex anchor `\z` to `$`. Expected: C05 and C17 FAIL. Revert with `git checkout -- SRC/Credentials/TeamsCredentialStore.cs`. Re-run Step 7. Expected: green.

- [ ] **Step 9: Push the branch (Pascal).** Expected: Task 2's floor and latest legs run both suites on each line, green.

- [ ] **Checkpoint (Pascal commits):** `git add SRC/Credentials SRC/Configuration TESTS` / suggested message `feat: package-owned Teams credentials from credential-helper-contract 1.0.0`

---

### Task 8: Escaping, scrubbing and the card envelope

**Re-verify at the live gate (mock-first, 2026-11-16):** backslash escapes vs the zero-width fallback, auto-link breaking per client, and the `summary` / `fallbackText` mapping (all S5).

**Files:**
- Create: `SRC/Cards/MarkdownEscaper.cs`, `SRC/Http/SecretScrubber.cs`, `SRC/Cards/CardEnvelope.cs`
- Test: `TESTS/MarkdownEscaperTests.cs`, `TESTS/SecretScrubberTests.cs`, `TESTS/CardEnvelopeTests.cs`

**Interfaces:**
- Produces:
  - `static string MarkdownEscaper.Escape(string? value)`: null gives `""`. Escapes the spec 7.3 characters **and** breaks auto-links (U+200B after the `:` of `://`, after `www.`, and before `@` in email-shaped tokens), per F03 and the Task 4 finding.
  - `static string SecretScrubber.Scrub(string? text, int maxLength = 200)`: strips control characters (`\p{Cc}`) and bidi controls (U+200E, U+200F, U+202A to U+202E, U+2066 to U+2069) first, then redacts, then truncates (F11).
  - `static JsonObject CardEnvelope.Wrap(JsonObject cardOrEnvelope)`: returns a `message` envelope as-is. Wraps an `AdaptiveCard` as `{"type":"message","attachments":[{"contentType":"application/vnd.microsoft.card.adaptive","content":…}]}`. Throws `ArgumentException` for anything else.
  - `static void CardEnvelope.ApplySummary(JsonObject envelope, string? summary)`: maps per the S5 finding. The default sets both the envelope `summary` and each card's `fallbackText`. Blank means no change.
  - `static (string Json, int Bytes) CardEnvelope.Serialize(JsonObject envelope)`: compact, UTF-8 byte count.

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData("[Click me](https://evil.example)", @"\[Click me\]\(https:​//evil.example\)")]
[InlineData(@"a\b*c_d#e>f-g`h", @"a\\b\*c\_d\#e\>f\-g\`h")]
public void Escape_EscapesSpecCharacters(string input, string expected) => Assert.Equal(expected, MarkdownEscaper.Escape(input));
// If Task 4 chose the zero-width fallback: replace the expectations with the fallback's rules, and keep the link case.

[Theory]   // F03: auto-link breaking, whatever Task 4 found about backslashes
[InlineData("see https://evil.example/x", "see https:​//evil.example/x")]
[InlineData("www.evil.example", "www.​evil.example")]
[InlineData("a@evil.example", "a​@evil.example")]
[InlineData("mailto:a@evil.example", "mailto:a​@evil.example")]
public void Escape_BreaksAutoLinks(string input, string expectedContains)
[Fact] public void Escape_SnapshotOfInjectionSet()   // https://evil.example/x, www.evil.example, a@evil.example, [x](https://evil.example), <at>x</at>, **bold**

[Fact] public void Scrub_RedactsUrlsAndSig_AndTruncates()
{ var s = SecretScrubber.Scrub($"bad {TestUrls.Sentinel} x sig=ABC&y " + new string('z', 500));
  Assert.DoesNotContain("SENTINEL", s); Assert.DoesNotContain("ABC", s); Assert.Contains("[redacted]", s); Assert.True(s.Length <= 200); }
[Fact] public void Scrub_StripsControlAndBidiCharacters()   // "‮abc\u0007" has neither U+202E nor U+0007

[Fact] public void Wrap_BareCard_IsWrappedOnce()
[Fact] public void Wrap_MessageEnvelope_IsUnchanged()            // no double wrap
[Fact] public void Wrap_OtherRoot_Throws()
[Fact] public void ApplySummary_SetsMappedFields_AndIgnoresBlank()
[Fact] public void Serialize_CountsUtf8Bytes()                    // a TextBlock with 1,000 "😀": Bytes == Encoding.UTF8.GetByteCount(Json) and > Json.Length
```

- [ ] **Step 2: Run, and check they fail.** Run: `dotnet test -p:AutomateMajor=18 --filter "MarkdownEscaperTests|SecretScrubberTests|CardEnvelopeTests"`. Expected: build errors.

- [ ] **Step 3: Implement.** In `Scrub`, strip control and bidi characters, redact `https?://\S+` and then `sig=[^&\s"]+`, then truncate to `maxLength`. Use `[GeneratedRegex]` methods (immutable, no static state).

- [ ] **Step 4: Run on every line, and check they pass.** Run: `./scripts/test.sh`. Expected: `Passed!` per line.

- [ ] **Checkpoint (Pascal commits):** `git add SRC/Cards SRC/Http TESTS` / suggested message `feat: markdown escaping, secret scrubbing and card envelope`

---

### Task 9: Value cap, payload fitter and link host policy (F01, F04)

**Re-verify at the live gate (mock-first, 2026-11-16):** the size limit Teams really enforces (`MaxPayloadBytes`).

**Files:**
- Create: `SRC/Cards/ValueLimiter.cs`, `SRC/Cards/PayloadFitter.cs`, `SRC/Cards/PersonalDataKeys.cs`, `SRC/Links/SiteHostProvider.cs`, `SRC/Links/LinkHostPolicy.cs`
- Modify: `SRC/Configuration/TeamsComposer.cs` (register `TimeProvider.System` with `TryAddSingleton`, `SiteHostProvider`, `LinkHostPolicy`)
- Test: `TESTS/ValueLimiterTests.cs`, `TESTS/PayloadFitterTests.cs`, `TESTS/PersonalDataKeysTests.cs`, `TESTS/LinkHostPolicyTests.cs`

**Interfaces:**
- Consumes: `CardEnvelope.Serialize` (Task 8), `HostNames` and `TeamsOptions` (Task 6).
- Produces:
  - `static (string Value, bool Truncated) ValueLimiter.Truncate(string? value, int maxLength)`: cuts on a grapheme boundary (`StringInfo`) at or below `maxLength` UTF-16 chars and appends `…`.
  - `record RenderOutput(JsonObject Envelope, IReadOnlyList<string> Warnings)`
  - `record FitResult(JsonObject? Envelope, int Bytes, IReadOnlyList<string> ShortenedKeys, IReadOnlyList<string> EmptiedKeys, IReadOnlyList<string> Warnings, bool TemplateTooBig)`
  - `static FitResult PayloadFitter.Fit(IReadOnlyDictionary<string, string> data, Func<IReadOnlyDictionary<string, string>, RenderOutput> render, int maxBytes)` with `const int MaxHalvingRounds = 64`. The end state (plan review P5, spec 4 and 7.4):
    1. Render `data`. Fits: return it.
    2. Render with every value `""`. Over: return `TemplateTooBig = true`, `Envelope = null`.
    3. Up to 64 rounds: halve the longest non-empty value (grapheme-safe, with `…`), re-render; return as soon as it fits.
    4. Empty values longest-first (current UTF-16 length, ties by ordinal key), re-render after each, until it fits. Step 2 guarantees this ends in a fit.
    `Warnings` = the final render's warnings, de-duplicated ordinally, then "Card value '{key}' was shortened to fit." per key in `ShortenedKeys`, then "Card value '{key}' was removed to fit." per key in `EmptiedKeys` (a key in `EmptiedKeys` is not also in `ShortenedKeys`). Keys only, never values.
  - `SiteHostProvider(IOptionsMonitor<WebRoutingSettings>, IDomainService, TimeProvider)` with `IReadOnlyList<(string Host, bool IsHttp)> GetSiteHosts()`: the `UmbracoApplicationUrl` host plus content-tree domain hosts, cached 5 minutes in an instance field.
  - `static IEnumerable<string> PersonalDataKeys.Warnings(IEnumerable<string> keys)`: "Card key '{key}' may hold personal data. See the GDPR notes." for keys containing `email`, `phone`, `mobile`, `address` or `postcode` as a whole word (split on case changes, `_`, `-`, `.`), or equal to `name`, `firstName`, `lastName` or `fullName`, case-insensitive (F13). Built-in template keys never warn.
  - `LinkHostPolicy(SiteHostProvider, IOptionsMonitor<TeamsOptions>)` with `LinkDecision Check(string? url, bool allowExternal)` → `Allowed`, or `Dropped(reason)`, where reason is one of "not an absolute https link", "links to a host outside AllowedLinkHosts". https only; http only when the matched site host is itself http.

- [ ] **Step 1: Write the failing tests**

```csharp
// ValueLimiterTests
[Fact] Truncate_UnderLimit_Unchanged()
[Fact] Truncate_OverLimit_EndsWithEllipsis_WithinLimitPlusOne()
[Fact] Truncate_NeverSplitsGrapheme()             // "👍🏽" and "é" sequences straddling the limit
[Fact] Truncate_100KbValue_IsAtMost2000()

// PayloadFitterTests (security review items 17, 18; plan review P5). Render = a FactSet of every key, through CardEnvelope.Serialize.
[Fact] Fit_100KbValue_SentWithinBudget()                    // ShortenedKeys == ["notes"], Bytes <= 28000
[Fact] Fit_TwentyKeysOf2000_FitsAndNamesShortenedKeys()
[Fact] Fit_EmojiAndControlChars_CountsEscapedUtf8Bytes()    // \u0001 serialises as 6 bytes; still fits
[Fact] Fit_50RowsOf2000SixByteEscapedChars_Fits()           // 50 x 2,000 x "\u0001" (about 600 KB serialised): Bytes <= 28000, TemplateTooBig false
[Fact] Fit_64RoundsExhausted_EmptiesLongestFirst()          // the same input: EmptiedKeys non-empty, ordered longest-first; Bytes <= 28000
[Fact] Fit_EscapeExpansionNearLimit_Fits()                  // render escapes data (2,000 "*" become 4,000 chars); values sized to land just over 28,000 after escaping
[Fact] Fit_AllEmptyFits_AlwaysReturnsFittingEnvelope()      // theory over the inputs above plus 1 row of 28,000 "é": Envelope not null, Bytes <= max
[Fact] Fit_WarningsFromFinalRender_Deduplicated()           // render adds "Unknown card key: x" each call: it appears exactly once
[Fact] Fit_EmptyDataTemplateTooBig_TemplateTooBigTrue()     // only case with Envelope null
[Fact] Fit_AlreadyFits_RendersOnce()

// PersonalDataKeysTests (F13)
[Theory, InlineData("email"), InlineData("contactEmail"), InlineData("phone_number"), InlineData("fullName")] Warns(string key)
[Theory, InlineData("formName"), InlineData("entryId"), InlineData("editor")] DoesNotWarn(string key)

// LinkHostPolicyTests (security review item 13)
[Fact] Check_SiteHost_Allowed()
[Fact] Check_ConfiguredAllowedLinkHost_Wildcard_Allowed()
[Fact] Check_ExternalHost_Dropped()
[Fact] Check_ExternalHost_AllowExternal_Allowed()
[Fact] Check_HttpExternal_Dropped()
[Fact] Check_HttpSiteHostOnLocalHttpSite_Allowed()
[Theory, InlineData("javascript:alert(1)"), InlineData("data:text/html,x"), InlineData("/relative"), InlineData("")]
Check_NonHttps_DroppedEvenWithAllowExternal(string url)
[Fact] Check_IdnLookAlikeOfSiteHost_Dropped()
[Fact] SiteHosts_CachedForFiveMinutes()            // FakeTimeProvider; IDomainService called once, then again after 5 minutes
[Fact] SiteHosts_NoneConfigured_EmptyList()
```

- [ ] **Step 2: Run, and check they fail.** Run: `dotnet test -p:AutomateMajor=18 --filter "ValueLimiterTests|PayloadFitterTests|PersonalDataKeysTests|LinkHostPolicyTests"`. Expected: build errors.

- [ ] **Step 3: Implement** to the Interfaces block. Register the three services in `TeamsComposer`.

- [ ] **Step 4: Run on every line, and check they pass.** Run: `./scripts/test.sh`. Expected: `Passed!` per line.

- [ ] **Checkpoint (Pascal commits):** `git add SRC/Cards SRC/Links SRC/Configuration TESTS` / suggested message `feat: value cap, payload fitter and link host policy`

---

### Task 10: Per-webhook rate limiter (F01)

**Re-verify at the live gate (mock-first, 2026-11-16):** Teams' real throttling (status, `Retry-After`) against the `RateLimit` defaults.

Before Task 11, which consumes it (plan review P11).

**Files:**
- Create: `SRC/Http/WebhookRateLimiter.cs`
- Modify: `SRC/Configuration/TeamsComposer.cs` (register it)
- Test: `TESTS/WebhookRateLimiterTests.cs`

**Interfaces:**
- Consumes: `TeamsOptions.RateLimit` (Task 6).
- Produces: `WebhookRateLimiter(IOptionsMonitor<TeamsOptions>, TimeProvider)`, a singleton, with `ValueTask<bool> WaitAsync(string normalisedCredentialName, TimeSpan maxWait, CancellationToken cancellationToken)`. One token bucket per normalised credential name in a `ConcurrentDictionary` instance field. `false` means no token within `maxWait`. Caller cancellation throws `OperationCanceledException`.

- [ ] **Step 1: Write the failing tests** (security review item 19)

```csharp
[Fact] Burst4_ThenThreePerSecond()                       // FakeTimeProvider: 4 immediate, the 5th waits ~333 ms
[Fact] TwentyConcurrent_NoMoreThan4InAnySecond()          // record grant times on the fake clock; sliding 1 s window max 4
[Fact] Wait_BeyondMaxWait_ReturnsFalse()
[Fact] Wait_BoundedByCallerTimeout()                     // maxWait 5 s, token cancelled at 1 s: throws, no grant consumed
[Fact] SeparateCredentialNames_SeparateBuckets()
[Fact] OptionsChange_AppliesToNewWaits()
```

(Case-insensitivity is the store's job now: callers pass the normalised name.)

- [ ] **Step 2: Run, and check they fail.** Run: `dotnet test -p:AutomateMajor=18 --filter WebhookRateLimiterTests`. Expected: build errors.

- [ ] **Step 3: Implement.** A small bucket of our own over `TimeProvider` (spec 7.8). Lock per bucket; waiters use `TimeProvider.Delay` so the fake clock drives them.

- [ ] **Step 4: Run on every line, and check they pass.** Run: `./scripts/test.sh`. Expected: `Passed!` per line.

- [ ] **Checkpoint (Pascal commits):** `git add SRC/Http SRC/Configuration TESTS` / suggested message `feat: per-webhook token bucket rate limiter`

---

### Task 11: Error classifier and webhook sender

**Re-verify at the live gate (mock-first, 2026-11-16):** the `202` empty-body answer, bare-text failure and every error status and body shape the classifier maps (S1).

**Files:**
- Create: `SRC/Http/TeamsErrorClassifier.cs`, `SRC/Http/TeamsPostResult.cs`, `SRC/Http/ITeamsWebhookSender.cs`, `SRC/Http/TeamsWebhookSender.cs`, `SRC/Http/TeamsPostException.cs`
- Create: `TESTS/Support/RecordingHandler.cs`, `TESTS/Support/CountingStream.cs`, `TESTS/Support/CaptureLoggerProvider.cs`, `TESTS/Support/TelemetryCapture.cs` (an `ActivityListener` on all sources plus a `DiagnosticListener` subscriber on `HttpHandlerDiagnosticListener` that records tags and the `ToString()` of every payload)
- Modify: `SRC/Configuration/TeamsComposer.cs` (register `ITeamsWebhookSender`)
- Test: `TESTS/TeamsErrorClassifierTests.cs`, `TESTS/TeamsWebhookSenderTests.cs`, `TESTS/SecretLeakTests.cs`

**Interfaces:**
- Consumes: `WebhookUrlValidator`, `ValidatedWebhookUrl`, `TeamsOptions` (Task 6); `TeamsCredentialStore`, `TeamsCredentialPart` (Task 7); `CardEnvelope`, `SecretScrubber` (Task 8); `WebhookRateLimiter` (Task 10).
- Produces:
  - `record TeamsFailure(StepRunErrorCategory Category, string Message)`
  - `static TeamsFailure? TeamsErrorClassifier.Classify(int statusCode, string? scrubbedBody)`: null for 2xx. Any 3xx gives `InvalidResponse` "Teams answered with a redirect. Check the webhook URL." (F10).
  - `record TeamsPostResult(bool Accepted, int StatusCode, int PayloadBytes, IReadOnlyList<string> Warnings, IReadOnlyList<string> TestWarnings, TeamsFailure? Failure)`. `TestWarnings` carries the host-naming text for the connection test only; actions never read it.
  - `interface ITeamsWebhookSender { Task<TeamsPostResult> PostAsync(string? credentialName, JsonObject envelope, CancellationToken cancellationToken); }`. **The sender takes the credential name, never a URL.**
  - `sealed class TeamsPostException(string message) : Exception(message)`. No constructor takes an inner exception.
  - `TeamsWebhookSender(IHttpClientFactory, IOptionsMonitor<TeamsOptions>, TeamsCredentialStore, WebhookUrlValidator, WebhookRateLimiter, TimeProvider, ILogger<TeamsWebhookSender>)`, a singleton.

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData(400, StepRunErrorCategory.InvalidResponse, "Teams rejected the card (400).")]   // + " " + scrubbed snippet when present
[InlineData(401, StepRunErrorCategory.Authentication, "Teams refused the webhook signature. Re-create the Workflow URL.")]
[InlineData(403, StepRunErrorCategory.Authentication, "Teams refused the webhook signature. Re-create the Workflow URL.")]
[InlineData(404, StepRunErrorCategory.ConfigurationError, "The workflow was deleted or turned off.")]
[InlineData(413, StepRunErrorCategory.Validation, "Teams says the message is too large.")]
[InlineData(429, StepRunErrorCategory.RateLimiting, "Teams is throttling this webhook. Use the step's Retry setting.")]
[InlineData(500, StepRunErrorCategory.ServiceUnavailable, "Teams returned 500. Try again later.")]
[InlineData(503, StepRunErrorCategory.ServiceUnavailable, "Teams returned 503. Try again later.")]
[InlineData(409, StepRunErrorCategory.InvalidResponse, "Teams returned 409.")]   // other non-2xx (architect's call)
[InlineData(307, StepRunErrorCategory.InvalidResponse, "Teams answered with a redirect. Check the webhook URL.")]
public void Classify_MapsStatus(int code, StepRunErrorCategory cat, string msg)

// TeamsWebhookSenderTests (RecordingHandler returns a set status and body, throws, or never completes; credentials via TestCredentials)
[Fact] Post_202_Accepted_RecordsJsonContentTypeAndBytes()      // Content-Type "application/json; charset=utf-8"; PayloadBytes == UTF-8 length
[Fact] Post_PostsOnlyToResolvedCredentialUrl()                  // council test 8: the handler saw exactly TestUrls.Sentinel, one request
[Fact] Post_MissingCredential_NoRequest_ConfigurationError()    // message == contract 1.3 text for ...:sentinel2:WebhookUrl
[Fact] Post_BadCredentialName_NoRequest_ConfigurationError()    // message == contract 1.2 text
[Fact] Post_GuardTripped_NoRequest_ConfigurationError()         // TestCredentials with "Umbraco:Automate": message == contract 1.4 text naming that entry
[Fact] Post_BlockedUrl_NoRequest_ConfigurationError()
[Fact] Post_UnknownHost_SendsAndAddsWarningWithoutHost()        // Warnings has the generic text; TestWarnings names the host
[Fact] Post_UnknownHost_StrictHosts_NoRequest()
[Fact] Post_OverMaxPayloadBytes_NoRequest_Validation()          // defence in depth; "Card is {n} bytes. Teams allows about 28 KB."
[Fact] Post_RateLimiterNoSlot_NoRequest_RateLimiting()          // "Too many posts to this Teams webhook at once. Use the step's Retry setting."
[Fact] Post_Timeout_IsTimeoutCategory()                         // P7: handler awaits Task.Delay(Infinite, ct); FakeTimeProvider advanced 1 s past TimeoutSeconds=1: "Teams didn't answer within 1 seconds."
[Fact] Post_CallerCancelled_ThrowsOperationCanceled()
[Fact] Post_HttpRequestException_ServiceUnavailable()          // "Couldn't reach Teams."
[Fact] Post_SsrfBlock_ConfigurationError()                      // 127.0.0.1 and 169.254.169.254 via the Task 5 seam: "The webhook host isn't allowed."
[Fact] Post_Redirect307_NoSecondRequest_InvalidResponse_NoLocation()   // F10, item 8
[Fact] Post_UsesNoRedirectClient()                              // the factory was asked for Constants.HttpClients.NoRedirect (or the Task 5 named client)
[Fact] Post_50MbBody_ReadsAtMost1024Bytes()                     // F11, item 9: CountingStream.BytesRead <= 1024 (P7: bytes read only, no memory assertion)
[Fact] Post_BodyWithBidiAndEmail_StrippedInMessage_NeverLogged()
[Fact] Post_LogsStatusAndBytesOnly()                            // P4: exactly one Information line, "Teams webhook returned 202 (N bytes)"; no host, no "powerplatform"
[Fact] ValidatedWebhookUrl_AsStructuredLogArgument_NoHost()     // P4: logger.LogInformation("{Url}", validated) via CaptureLoggerProvider: no host, path or query

// SecretLeakTests
[Theory, MemberData(nameof(EveryOutcome))]  // 202, 307, 400 whose body echoes TestUrls.Sentinel, 401, 404, 429, 500, timeout, HttpRequestException whose message holds the URL
Sender_NoOutcome_LeaksUrl()   // captured logs (Trace), TelemetryCapture tags and payload strings, result Warnings, TestWarnings and Failure.Message contain neither SENTINELPATH nor SENTINELSIG
[Fact] Sender_SentinelHost_NeverInRunTimeOutput()  // credential https://SENTINELSIG.example.org/...: no log, Warnings or Failure text holds it (item 4)
[Fact] FrameworkHttpLogging_AtTrace_NeverContainsSig()
// A real ServiceCollection: AddLogging(capture, Trace) + Umbraco's NoRedirect client registration (or the Task 5 named client),
// primary handler stubbed. Proves framework logging redacts, or that our client removes it.
[Fact] DiagnosticListenerPayloadObject_HoldsRequest_KnownResidualR2()
// Documents the limit (F07): asserts the payload object IS an HttpRequestMessage, so a future change is noticed. Not a leak test.
```

- [ ] **Step 2: Run, and check they fail.** Run: `dotnet test -p:AutomateMajor=18 --filter "TeamsErrorClassifierTests|TeamsWebhookSenderTests|SecretLeakTests"`. Expected: build errors.

- [ ] **Step 3: Implement `PostAsync`**

Order: `store.TryGet(credentialName, TeamsCredentialPart.WebhookUrl, out url, out error)` (guard, name and read inside the store; on false, `ConfigurationError` with `error` verbatim); `store.TryNormaliseName` for the limiter key; validate the URL; serialise and measure; wait on the rate limiter (bounded by `MaxWaitSeconds` and the remaining timeout); POST with `HttpCompletionOption.ResponseHeadersRead` and a `CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds), timeProvider)` linked to the caller's token. On a non-2xx, read at most 1,024 bytes from the content stream, then dispose. Then `Scrub`, then `Classify`. Log one Information line: `Teams webhook returned {StatusCode} ({Bytes} bytes)`. Never log the host, the body or the value.

Catch `OperationCanceledException` when the caller's token is cancelled and rethrow it. Treat any other cancellation as our timeout. Never touch `ex.Message` (a CodeQL or review check). Use `Constants.HttpClients.NoRedirect`, or the named client if Task 5 says so. Register the sender in `TeamsComposer`.

- [ ] **Step 4: Run on every line, and check they pass.** Run: `./scripts/test.sh`. Expected: `Passed!` per line.

- [ ] **Checkpoint (Pascal commits):** `git add SRC/Http SRC/Configuration TESTS` / suggested message `feat: Teams webhook sender with error mapping and secret-leak tests`

---

### Task 12: Connection test, saving handler and architecture tests

**Re-verify at the live gate (mock-first, 2026-11-16):** the Test connection result against a real `202` (S1).

**Files:**
- Modify: `SRC/Connection/TeamsWorkflowConnectionType.cs`, `SRC/Configuration/TeamsComposer.cs` (the saving handler)
- Create: `SRC/Connection/TeamsConnectionSavingHandler.cs` (only if Task 5 S3 found a usable hook)
- Test: `TESTS/TeamsWorkflowConnectionTypeTests.cs`, `TESTS/TeamsConnectionSavingHandlerTests.cs`, `TESTS/ArchitectureTests.cs`

**Interfaces:**
- Consumes: `ITeamsWebhookSender` (Task 11), `TeamsCredentialStore`, `TeamsCredentialSourceCheck` (Task 7), `SiteHostProvider` (Task 9), `CardEnvelope` (Task 8).
- Produces:
  - Constructor `TeamsWorkflowConnectionType(ConnectionTypeInfrastructure, ITeamsWebhookSender, TeamsCredentialStore, TeamsCredentialSourceCheck, SiteHostProvider, IOptionsMonitor<WebRoutingSettings>)`. It calls `TryNormaliseName` (for the source check) and never `TryGet`.
  - `ConnectionTypeBase` has no `SetupDocsUrl`. Put `TeamsDocs.SetupUrl` in the connection `Description` and in the `CredentialName` field description.
  - `TeamsConnectionSavingHandler(TeamsCredentialStore) : INotificationHandler<ConnectionSavingNotification>`: for connections of our type, cancels the save with the store's name error when `TryNormaliseName` fails. Never echoes the value.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] Validate_Accepted_PostsTestCard_AndSaysConnected()
// The envelope text contains "Connection test from {site}"; message starts "Connected (Teams accepted the message)."
// and includes "Anyone who can build automations in a workspace that allows this connection can post to this channel."
// Site = the host of UmbracoApplicationUrl, or "your Umbraco site" when unset.
[Fact] Validate_UnknownHost_ConnectedPlusWarning()
// Message contains "Connected, but hooks.example.org isn't a known Power Automate host. Check the URL." Status per the Task 5 S3 decision.
[Fact] Validate_UnknownNonDnsHost_GenericHostText()
[Fact] Validate_StrictHosts_UnknownHost_Failure_NoRequest()                  // P10
[Fact] Validate_MissingCredential_Failure_NoRequest_NamesKeyOnly()           // council test 7: contract 1.3 text
[Theory, InlineData("https://x.example/a?sig=SENTINELSIG"), InlineData("$Umbraco:Automate:Secrets:X"), InlineData("ops-alerts"), InlineData("alerts\n")]
Validate_BadCredentialName_Failure_NoRequest_NoEcho(string name)              // contract 1.2 text; no part of the input in Message or Details
[Fact] Validate_GuardTripped_Failure_NoRequest()                             // contract 1.4 text
[Fact] Validate_RetiredUrl_Failure_NoRequest()
[Fact] Validate_HttpUrl_Failure_NoRequest()
[Fact] Validate_404_FailureWithMappedMessage()
[Fact] Validate_CredentialFromAppsettingsJson_AddsMoveItWarning()           // council condition 7, via TeamsCredentialSourceCheck
[Fact] Validate_NoSiteHosts_AddsButtonsDroppedWarning()
[Fact] Validate_HandlerThrowsWithSentinelInMessage_ReturnsOurText()         // item 3: Core's ex.Message fallback never reached
[Fact] Validate_NoResultText_ContainsUrl()                                  // Message and Details are free of SENTINEL*

// TeamsConnectionSavingHandlerTests (if the hook exists)
[Theory, InlineData("https://x.example/a?sig=S"), InlineData("$Umbraco:Automate:Secrets:X"), InlineData("a:b"), InlineData("ops-alerts")]
Saving_BadName_Cancelled_MessageDoesNotEcho(string name)
[Fact] Saving_GoodName_NotCancelled()                                        // "Alerts" and " alerts "
[Fact] Saving_OtherConnectionType_Ignored()

// ArchitectureTests (reflection over the package assembly)
[Fact] NoSettingsType_HasSensitiveField()          // D9: no [Field(IsSensitive = true)] anywhere
[Fact] NoStaticField_HoldsScopedOrMutableState()   // P2: every static field is const or readonly, and of type string, Regex or IReadOnlyList<string>
[Fact] Singletons_DependOnlyOnSingletonSafeTypes() // ctor params of every service TeamsComposer registers are in an allow-list: IHttpClientFactory, IConfiguration,
                                                   // IOptionsMonitor<>, ILogger<>, TimeProvider, IDomainService, and our own singletons
[Fact] Assembly_HasNoControllersOrViews()          // S1/S5/P5: no ControllerBase, Controller or SurfaceController subclasses; no Razor views
[Fact] NoSettings_HasWebhookStyleUrlProperty()     // properties with "Url" in the name are only PostMessageSettings.LinkUrl
[Fact] OnlySender_CallsCredentialStoreTryGet()     // IL scan: only TeamsWebhookSender calls TeamsCredentialStore.TryGet
[Fact] TeamsPostException_HasNoInnerExceptionConstructor()
```

- [ ] **Step 2: Run, and check they fail.** Run: `dotnet test -p:AutomateMajor=18 --filter "TeamsWorkflowConnectionTypeTests|TeamsConnectionSavingHandlerTests|ArchitectureTests"`. Expected: build errors or failures.

- [ ] **Step 3: Implement `ValidateAsync`.** Build a small Simple-style card, `Wrap` it, then call `PostAsync(settings.CredentialName, ...)`. Map the result: blocked or failed gives `Failure(message)`, accepted gives `Success`/`Warning` as above, using `TestWarnings` for the host text. Add the appsettings-file warning (when `TryNormaliseName` succeeds and `AppsettingsFileSupplying` returns a file) and the no-site-hosts warning. Catch everything except caller cancellation and return our own text. Register the saving handler in `TeamsComposer`.

- [ ] **Step 4: Run on every line, and check they pass.** Run: `./scripts/test.sh`. Expected: `Passed!` per line.

- [ ] **Checkpoint (Pascal commits):** `git add SRC TESTS` / suggested message `feat: Teams connection test, saving handler and architecture guards`

---

### Task 13: Post message action and the frozen contract

**Files:**
- Create: `SRC/Actions/MessageCardBuilder.cs`, `SRC/Actions/ConnectionChoiceValidator.cs`
- Modify: `SRC/Actions/PostMessageAction.cs`, `SRC/Actions/PostMessageSettings.cs` (field labels, descriptions, sort order, defaults)
- Test: `TESTS/MessageCardBuilderTests.cs`, `TESTS/PostMessageActionTests.cs`, `TESTS/ConnectionChoiceValidatorTests.cs`, `TESTS/ContractTests.cs`, `TESTS/Contract/frozen-contract.json`

**Interfaces:**
- Consumes: `ITeamsWebhookSender`, `TeamsPostResult`, `TeamsPostException` (Task 11); `MarkdownEscaper`, `CardEnvelope` (Task 8); `ValueLimiter`, `PayloadFitter`, `RenderOutput`, `LinkHostPolicy`, `PersonalDataKeys` (Task 9).
- Produces:
  - `static RenderOutput MessageCardBuilder.Build(PostMessageSettings settings, IReadOnlyDictionary<string, string> data, string cardVersion, LinkHostPolicy links)`. **Escape order (plan review P9, spec 7.3):** split Title and Message into literal segments and `{{key}}` placeholders; in Plain mode escape each literal segment, in Markdown mode leave it; replace each placeholder once with `MarkdownEscaper.Escape(value)`. Drops the button per `LinkHostPolicy`. Returns the wrapped envelope and its warnings.
  - `static TeamsPostOutput TeamsPostOutput.From(TeamsPostResult result, IEnumerable<string> extraWarnings)` (ignores `TestWarnings`).
  - `ConnectionChoiceValidator`: shared by both actions. `IReadOnlyList<string> Validate(step, workspace)` returns "Choose which Teams connection this step posts to." when the step has no `ConnectionId` and the workspace allows more than one `community.teams.workflowWebhook` connection. The exact inputs follow the Task 3 F06 route.
  - `PostMessageAction` also implements `IValidatableStepType` (blank `Message` field: "Message is required.") and `IPublishValidatableStepType` (the connection choice rule).
  - Field descriptions: `Formatting` Markdown option: "Bound data in Title and Message isn't escaped. Put untrusted values in Data instead." `Data`: "Values are escaped and shown in the run log to workspace members." `Title` and `Message`: "A {{key}} here is filled from Data, including one that arrives in bound text." `AllowExternalLinks`: "Allow the button to link to sites other than this one."

- [ ] **Step 1: Write the failing tests**

```csharp
// MessageCardBuilderTests
[Fact] Build_TitleAsBoldHeading_MessageAsTextBlock_StyleOnContainer()
[Fact] Build_PlainText_EscapesTitleAndMessage()           // "[Click](https://evil.example)" is escaped; "https://evil.example/login" is link-broken
[Fact] Build_PlainText_EachCharacterEscapedOnce()         // P9: Message "a*b {{k}}", data k="c*d" -> text == @"a\*b c\*d" (no "\\*")
[Fact] Build_PlainText_PlaceholderKeyWithUnderscore_Substituted()   // P9: "{{form_name}}" still matches in Plain mode
[Fact] Build_Markdown_LeavesAuthorMarkdown()
[Fact] Build_Markdown_DataPlaceholder_EscapedOnce()       // Message "**Hi** {{name}}", data name="[x](https://evil.example)": bold stays, data escaped once
[Fact] Build_BoundTitleContainingPlaceholder_IsSubstituted_Accepted()   // P9, accepted low: bound Title "{{email}}" + Data email=a@b.example -> escaped value appears
[Fact] Build_DataContainingPlaceholder_IsNotReSubstituted()
[Fact] Build_UnknownDataKey_EmptyAndWarning()
[Fact] Build_LinkUrlOnSiteHost_AddsOpenUrl_DefaultLabelOpen()
[Theory, InlineData("/news/item"), InlineData("javascript:alert(1)"), InlineData("ftp://x"), InlineData("https://evil.example/x"), InlineData("http://evil.example/x")]
Build_BadOrExternalLinkUrl_DropsButton_AddsWarning(string link)
[Fact] Build_ExternalLink_AllowExternalLinks_Kept()
[Fact] Build_SetsCardVersion()
[Fact] Settings_DefaultFormatting_IsPlainText()           // item 12

// PostMessageActionTests (ActionTestHarness + RecordingHandler-backed sender + TestCredentials + the Task 3 context-logger route)
[Fact] Execute_BoundNameWithQuotesBackslashBrace_ValidJson_TextExact()  // name = "Rock \"n\" Roll \\ }"
[Theory, InlineData(""), InlineData("   ")] Execute_BlankBoundMessage_PostsPlaceholder(string m)   // "(no message text)" sent, warning in output (F01)
[Fact] Save_BlankMessageField_Error()                                  // "Message is required."
[Fact] Execute_MissingCredential_FailsConfigurationError_NoRequest()   // contract 1.3 text
[Fact] Execute_429_FailsRateLimiting()
[Fact] Execute_RateLimiterFull_FailsRateLimiting()                     // P10: limiter pre-drained on the fake clock; no request
[Fact] Execute_404_FailsConfigurationError_WithDeletedMessage()
[Fact] Execute_100KbBoundMessage_SentWithinBudget_WarningNamesField()  // F01, item 17: success, Bytes <= MaxPayloadBytes
[Fact] Execute_EmojiPayloadOverLimit_ShortenedAndSent()                // fewer than 28,000 chars, more than 28,000 bytes
[Theory, MemberData(nameof(AdversarialData))]                         // P5: 100 KB, 50 x 2,000 "\u0001", 2,000 "*" rows, emoji
Execute_AdversarialData_NeverFailsValidation(...)                     // result is success and no Validation category, for data-only variations
[Fact] Execute_RelativeLink_PostsWithoutButton_WarningInOutput()
[Fact] Execute_PersonalDataDataKey_WarningInOutput()                   // Data key "email" (F13)
[Fact] Execute_Success_OutputHasStatusAcceptedBytes()
[Theory, MemberData(nameof(EveryFailureOutcome))]                     // P8: missing credential, bad name, guard tripped, blocked URL, 400 echoing the URL, 401, 404, 429, 500, timeout, HttpRequestException with the URL, limiter full
Execute_EveryFailure_RunLogAndExceptionFreeOfSentinel_NullInner(...)  // the step's context logger, ActionResult exception ToString(): no SENTINELPATH/SENTINELSIG; InnerException null
[Fact] Execute_NoConnectionIdSeveralTeamsConnections_RunTimeWarning()  // P10: only if Task 3 recorded "F06 publish validation not possible" (spec 8 fallback); otherwise omit

// ConnectionChoiceValidatorTests (F06, item 21)
[Fact] TwoTeamsConnections_NoConnectionId_Blocks()
[Fact] TwoTeamsConnections_ConnectionIdSet_Allows()
[Fact] OneTeamsConnection_NoConnectionId_Allows()
[Fact] PostMessage_PublishValidation_UsesRule()

// ContractTests
[Fact] AliasesSettingsAndTemplateKeys_MatchFrozenContract()
// Reads every [ConnectionType]/[Action] alias and every settings property name; compares them with frozen-contract.json.
// Task 16 adds the PostCard entries and the template keys. The file's header comment says: "Changing this file breaks saved automations."
```

- [ ] **Step 2: Run, and check they fail.** Run: `dotnet test -p:AutomateMajor=18 --filter "MessageCardBuilderTests|PostMessageActionTests|ConnectionChoiceValidatorTests|ContractTests"`. Expected: failures.

- [ ] **Step 3: Implement `ExecuteAsync`.** If the bound `Message` is blank, use "(no message text)" and add a warning. Cap `Title`, `Message` and every `Data` value with `ValueLimiter`. Use `PayloadFitter.Fit` with a render function over `MessageCardBuilder.Build` at the connection's `DefaultCardVersion`; `Title` and `Message` count as data values for shrinking. If `TemplateTooBig`, fail with `Validation` (spec 7.4 message) and send nothing. Otherwise call `PostAsync(connection.CredentialName, fit.Envelope, ...)`, then return `Success(TeamsPostOutput.From(result, fit.Warnings + personal-data warnings))` or `ActionResult.Failed(new TeamsPostException(f.Message), f.Category)`. Log warnings at Warning level (text only, never a value or the link).

- [ ] **Step 4: Run on every line.** Run: `./scripts/test.sh`. Expected: `Passed!` per line. **M1 done.**

- [ ] **Checkpoint (Pascal commits):** `git add SRC TESTS` / suggested message `feat: Post Teams message action`

---

## M2: Post Adaptive Card

### Task 14: `{{key}}` card renderer

**Files:**
- Create: `SRC/Cards/CardRenderer.cs`, `SRC/Cards/UrlProperties.cs`, `TESTS/Contract/adaptive-card-1.5.schema.json`
- Modify: `SRC/Configuration/TeamsComposer.cs` (register `ICardRenderer`, P11)
- Test: `TESTS/CardRendererTests.cs`, `TESTS/UrlPropertiesTests.cs`

**Interfaces:**
- Consumes: `MarkdownEscaper`, `CardEnvelope` (Task 8); `LinkHostPolicy`, `PersonalDataKeys`, `RenderOutput` (Task 9).
- Produces:
  - `record CardRenderOptions(bool EscapeMarkdown, string DefaultCardVersion, string? Summary, bool AllowExternalLinks)`
  - `record CardRenderResult(JsonObject Envelope, IReadOnlyList<string> Warnings)` (converts to `RenderOutput` for the fitter)
  - `interface ICardRenderer { CardRenderResult Render(JsonNode template, IReadOnlyDictionary<string, string> data, CardRenderOptions options); }`
  - `CardRenderer(LinkHostPolicy) : ICardRenderer`, a singleton. Copies `data` into a `StringComparer.OrdinalIgnoreCase` dictionary.
  - `static bool UrlProperties.IsUrlProperty(string propertyName, JsonNode parent)`: the explicit, case-insensitive list in spec 7.3 (F05): `url`, `*Url`, `backgroundImage` (string form and `.url`), `poster`, `sources[].url`, `selectAction.url`.
  - The placeholder regex is a `[GeneratedRegex(@"\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]`.
  - Deliberately differs from the spec's sketch: the result is the envelope, not JSON plus bytes, because the sender measures once for all callers.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] Render_ReplacesPlaceholdersInStringValuesOnly()        // keys and numbers untouched; "{{title}}" as a property name stays
[Fact] Render_MixedTextAndSeveralPlaceholders()               // "Now live: {{title}} by {{ editor }}"
[Fact] Render_KeyMatchIsCaseInsensitive()
[Fact] Render_UnknownKey_EmptyAndWarning()                    // Warnings contains "Unknown card key: missing"
[Theory, MemberData(nameof(AwkwardStrings))]                  // P14 replaces FsCheck: quotes, braces, backslashes, lone surrogate, U+2028, NUL, "$1", emoji, "\r\n"
Render_AwkwardStrings_ValidJson_ValueIntact(string value)     // escaping off: parse the output, read the TextBlock, equals the input
[Fact] Render_EscapeOn_EscapesDataNotTemplateText()           // the author's **bold** stays; data "[x](y)" is escaped
[Fact] Render_EscapeOn_BreaksBareLinksInData()                // item 11: https://, www., @ in data get U+200B; template text untouched
[Fact] Render_UrlProperties_NotEscaped()                      // every name in UrlProperties, any case
[Theory, InlineData("/relative"), InlineData("javascript:x"), InlineData(""), InlineData("https://evil.example/x"), InlineData("http://evil.example/x")]
Render_BadOrExternalUrlInOpenUrl_RemovesAction_AddsWarning(string v)
[Fact] Render_ExternalUrl_AllowExternalLinks_Kept()
[Fact] Render_BadImageUrl_RemovesElement_AddsWarning()
[Fact] Render_BackgroundImageAndPosterPlaceholders_UrlChecked_NotEscaped()   // item 14, F05
[Fact] Render_EnvelopeWithTwoAttachments_RulesApplyToBoth()   // item 15: one Adaptive Card, one other contentType
[Fact] Render_DataContainingPlaceholder_IsNotReSubstituted()  // title="{{secret}}", secret="x": the output contains "{{secret}}"
[Fact] Render_KeysWithRegexSpecialCharacters_Literal()        // item 16: keys "a.b", "a-b"; data values containing "$1", "\\", "(?"
[Fact] PlaceholderRegex_HasMatchTimeout()                     // P7: MatchTimeout == 1 s (replaces the 10k wall-clock test)
[Fact] Render_PersonalDataKey_WarningNamesKey()               // F13, via PersonalDataKeys
[Fact] Render_KeepsAuthorVersion_SetsDefaultOnlyWhenMissing()
[Fact] Render_FullEnvelope_NotDoubleWrapped()
[Fact] Render_DoesNotMutateTemplate()
[Fact] Render_AppliesSummary()

// UrlPropertiesTests (F05, item 14)
[Fact] EverySchemaUriProperty_IsOnTheList()   // walk Contract/adaptive-card-1.5.schema.json (source URL and date in a header comment):
                                              // every property with "format": "uri" or "uri-reference" matches IsUrlProperty, case-insensitively
```

- [ ] **Step 2: Run, and check they fail.** Run: `dotnet test -p:AutomateMajor=18 --filter "CardRendererTests|UrlPropertiesTests"`. Expected: build errors.

- [ ] **Step 3: Implement.** Deep-clone the template. Walk the tree with a single `Regex.Replace` per string value; the replacement output is never re-scanned, which is what makes the no-re-substitution test pass. Walk every attachment's `content` when the root is a `message` envelope. For URL properties, run `LinkHostPolicy.Check(value, options.AllowExternalLinks)` after substitution. Remove the offending `Action.OpenUrl` from its `actions` array, or the element from its container (or the property, for `backgroundImage`). Then set the version, `Wrap`, and `ApplySummary`. Fetch the 1.5 schema once from `adaptivecards.io` into `TESTS/Contract/`. Register `ICardRenderer` in `TeamsComposer`.

- [ ] **Step 4: Run on every line, and check they pass.** Run: `./scripts/test.sh`. Expected: `Passed!` per line.

- [ ] **Checkpoint (Pascal commits):** `git add SRC/Cards SRC/Configuration TESTS` / suggested message `feat: JSON-safe {{key}} card renderer`

---

### Task 15: Built-in templates

**Re-verify at the live gate (mock-first, 2026-11-16):** the card version and how each template renders on desktop, web and mobile (S5). Keep the templates as JSON data files, so rework is an edit, not code.

**Files:**
- Modify: `SRC/Cards/Templates/*.json` (the Task 4 drafts, finalised with the S5 decisions), the package csproj (`EmbeddedResource`), `SRC/Configuration/TeamsComposer.cs` (register `CardTemplates`, P11)
- Create: `SRC/Cards/CardTemplates.cs`, `TESTS/Contract/adaptive-card-1.4.schema.json`
- Test: `TESTS/CardTemplatesTests.cs`, `TESTS/Contract/Snapshots/*.json`

**Interfaces:**
- Consumes: `ICardRenderer` (Task 14).
- Produces:
  - `enum CardSource { ContentPublished, ApprovalNeeded, FormSubmitted, Simple, Custom }`
  - `CardTemplates` (singleton) with `JsonNode Get(CardSource source)` (a clone of a cached, lazily parsed tree, P7), `IReadOnlyList<string> RequiredKeys(CardSource source)`, `IReadOnlyList<string> AllKeys(CardSource source)` and `string DisplayName(CardSource source)`: "Content published", "Approval needed", "Form submitted" or "Simple".
  - Keys:

| Template | Required keys | Optional keys | Button label |
|---|---|---|---|
| Content published | `title`, `url` | `contentType`, `editor`, `publishedAt` | "View page" |
| Approval needed | `title`, `url` | `editor`, `requestedAt`, `notes` | "Open in backoffice" |
| Form submitted | `formName`, `url` | `entryId`, `submittedAt` | "View entry" |
| Simple | `title`, `text` | `url`, `linkLabel` | `{{linkLabel}}` |

  Headings: "Now live: {{title}}", "Approval needed: {{title}}", "New entry: {{formName}}". Card version `1.4`, unless S5 changed it.

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory, MemberData(nameof(BuiltIns))] Template_RendersToSnapshot(CardSource s)        // sample data -> JsonNode.DeepEquals(snapshot)
[Theory, MemberData(nameof(BuiltIns))] Template_ValidatesAgainstAdaptiveCard14Schema(CardSource s)
[Theory, MemberData(nameof(BuiltIns))] Template_UsesOnlyAllowedElements(CardSource s) // TextBlock, FactSet, ColumnSet, Action.OpenUrl, plus Fact/Column children
[Theory, MemberData(nameof(BuiltIns))] Template_ContainsOnlyItsDeclaredKeys(CardSource s)
[Fact] FormSubmitted_ExtraEmailRow_NeverInOutput()                                   // data has email=a@b.example: the output has no "a@b.example"
[Fact] Get_ReturnsIndependentClones()
[Fact] NoTemplate_UsesClickHereLabel()
[Fact] Simple_WithoutUrl_HasNoButton()
[Theory, MemberData(nameof(BuiltInsWithUrl))] BuiltIn_ExternalUrl_ButtonDropped_EvenIfAllowExternalLinks(CardSource s)  // F04
[Theory, MemberData(nameof(BuiltIns))] BuiltIn_KeysNeverTriggerPersonalDataWarning(CardSource s)
```

- [ ] **Step 2: Run, and check they fail.** Run: `dotnet test -p:AutomateMajor=18 --filter CardTemplatesTests`. Expected: failures.

- [ ] **Step 3: Implement `CardTemplates` and embed the JSON.** Fetch the 1.4 schema once from `adaptivecards.io` into `Contract/`, with the source URL and date in a comment at the top of the test. Register `CardTemplates` in `TeamsComposer`.

- [ ] **Step 4: Run on every line, and check they pass.** Run: `./scripts/test.sh`. Expected: `Passed!` per line.

- [ ] **Checkpoint (Pascal commits):** `git add SRC TESTS` / suggested message `feat: four built-in Adaptive Card templates`

---

### Task 16: Post Adaptive Card action with save and publish validation

**Re-verify at the live gate (mock-first, 2026-11-16):** card version, `width: Full` and the rendered result of each action on all three clients (S5).

**Files:**
- Create: `SRC/Actions/PostCardAction.cs`, `SRC/Actions/PostCardSettings.cs`
- Modify: `TESTS/Contract/frozen-contract.json`
- Test: `TESTS/PostCardActionTests.cs`, `TESTS/PostCardValidationTests.cs`

**Interfaces:**
- Consumes: `ICardRenderer` (Task 14), `CardTemplates` (Task 15), `ITeamsWebhookSender` (Task 11), `TeamsPostOutput.From` and `ConnectionChoiceValidator` (Task 13), `ValueLimiter`, `PayloadFitter`, `PersonalDataKeys` (Task 9), `WebhookRateLimiter` (Task 10, for the shared-bucket test).
- Produces:
  - `PostCardSettings { CardSource CardSource = Simple; string? CustomCardJson; List<CardDataRow> CardData = []; string? Summary; bool EscapeMarkdownInData = true; bool AllowExternalLinks; }`
    - `CustomCardJson` uses the JSON code editor, has bindings **off**, and sets `VisibleWhen = nameof(CardSource)`, `VisibleWhenValues = [nameof(CardSource.Custom)]`.
    - `CardData` uses `UmbracoAutomate.PropertyEditorUi.KeyValueEditor` with bindings on. Description includes "Values are shown in the run log to workspace members." (F13)
    - `EscapeMarkdownInData` and `AllowExternalLinks` sit in `Group = "Advanced"`. `AllowExternalLinks` is visible only for Custom and is ignored for built-in templates (F04).
  - `PostCardAction : ActionBase<PostCardSettings, TeamsPostOutput>, IValidatableStepType, IPublishValidatableStepType`, alias `community.teams.postCard`, display name "Post Teams Adaptive Card".
  - **Spec gap, handled here:** `ValidateSettingsAsync` returns a list where every string blocks the save (Core `AutomationService`). Errors only, at save. The spec's save-time *warnings* (an unknown `{{key}}`, `Action.Submit`) move to run time, in `Warnings` plus an `ILogger` warning.
  - **Ignored rows on built-ins (P10):** a `CardData` row whose key isn't in `CardTemplates.AllKeys(source)` is not sent and adds "Card key '{key}' isn't used by the {DisplayName} template, so it wasn't sent." Personal-data warnings run only on keys that are sent.

- [ ] **Step 1: Write the failing tests**

```csharp
// PostCardValidationTests
[Fact] Save_CustomInvalidJson_Error()
[Fact] Save_CustomRootNotCardOrEnvelope_Error()
[Fact] Save_DuplicateKeysCaseInsensitive_Error()                 // "Title" and "title"
[Fact] Save_CustomUnknownPlaceholder_NoError()                   // it is a run-time warning instead
[Fact] Save_BuiltIn_NoErrors()
[Fact] Publish_ApprovalNeededWithoutUrl_Error()                  // "Approval needed card needs a url value"
[Theory, MemberData(nameof(RequiredKeyCases))] Publish_MissingRequiredKey_Error(CardSource s, string key)  // "{DisplayName} card needs a {key} value"
[Fact] Publish_TwoTeamsConnections_NoConnectionId_Error()       // F06, item 21
[Fact] Publish_OneTeamsConnection_NoConnectionId_NoError()

// PostCardActionTests
[Fact] Execute_PageNameWithQuoteAndBrace_ValidJsonRendered()
[Fact] Execute_FormValueLink_EscapingOn_Literal()
[Fact] Execute_FormValueBareUrl_EscapingOn_LinkBroken()         // F03
[Fact] Execute_FormValueLink_EscapingOff_Markdown()
[Fact] Execute_CustomMissingKey_RendersEmpty_WarningNamesKey()
[Fact] Execute_CustomActionSubmit_WarningInOutput()
[Fact] Execute_CustomFullEnvelope_SentAsIs()
[Fact] Execute_CustomCardEmailKey_PersonalDataWarningInOutput()  // P10: action level
[Fact] Execute_FormSubmittedIgnoredEmailRow_NotSent_IgnoredRowWarning_NoPersonalDataWarning()   // P10
[Fact] Execute_ApprovalNeeded_ExternalUrl_ButtonDropped()       // F04
[Fact] Execute_100KbCardDataValue_SentWithinBudget_WarningNamesKey()   // F01, item 17
[Fact] Execute_TwentyKeysOf2000_SentWithinBudget()              // F01, item 18
[Theory, MemberData(nameof(AdversarialData))] Execute_AdversarialData_NeverFailsValidation(...)   // P5, as Task 13
[Fact] Execute_CustomTemplateTooBigWhenEmpty_FailsValidation_NoRequest()
[Fact] Execute_CustomInvalidJsonAtRunTime_FailsValidation_NoRequest()  // defence in depth if the save check is bypassed
[Fact] Execute_RateLimiterFull_FailsRateLimiting()              // P10
[Fact] TwoActions_SameCredential_ShareBucket()                  // P10: one WebhookRateLimiter; 4 Post message sends drain the burst; a Post card send on the same credential waits on the fake clock and fails RateLimiting at MaxWaitSeconds
[Theory, MemberData(nameof(EveryFailureOutcome))]
Execute_EveryFailure_RunLogAndExceptionFreeOfSentinel_NullInner(...)   // P8, as Task 13
```

- [ ] **Step 2: Run, and check they fail.** Run: `dotnet test -p:AutomateMajor=18 --filter "PostCard|TwoActions"`. Expected: build errors.

- [ ] **Step 3: Implement.** Pick the template (built-in through `CardTemplates.Get`, or Custom by parsing `CustomCardJson`). Build `data` from the rows; for built-ins, drop rows not in `AllKeys` with the ignored-row warning. Cap every value with `ValueLimiter`. Render through `PayloadFitter.Fit` with `CardRenderOptions(EscapeMarkdownInData, connection.DefaultCardVersion, Summary, AllowExternalLinks && CardSource == Custom)`. If `TemplateTooBig`, fail with `Validation` and send nothing. Add the `Action.Submit` warning, `PostAsync(connection.CredentialName, ...)`, and map the result as in Task 13. Publish validation adds `ConnectionChoiceValidator`. Add the PostCard alias, the settings names (including `AllowExternalLinks`) and the template keys to `frozen-contract.json`.

- [ ] **Step 4: Run on every line.** Run: `./scripts/test.sh`. Expected: `Passed!` per line. **M2 done.**

- [ ] **Checkpoint (Pascal commits):** `git add SRC TESTS` / suggested message `feat: Post Teams Adaptive Card action with save and publish validation`

---

## M3: Packaging and docs

### Task 17: appsettings schema, packaging checks and the release workflow (F08)

CI already runs from Task 2. This task adds what a package needs and Jira's release workflow, which only publishes once the repo is public (Task 20).

**Files:**
- Create: `SRC/UmbracoCommunityAutomateTeamsSchema.cs`, `SRC/buildTransitive/Umbraco.Community.Automate.MicrosoftTeams.props`, `umbraco-marketplace.json`
- Copy from Jira's `main` (Jira Task 19): `.github/workflows/pack-release.yml`, `scripts/pack.sh`, `scripts/check-packed-readme.sh`
- Modify: the package csproj (the `JsonSchemaGenerate` target as in Slack's csproj; `Umbraco.JsonSchema.Extensions` as a private global package reference), `.github/workflows/build-test.yml` (Jira's packed-README step on the floor leg)

**Interfaces:**
- Produces:
  - `appsettings-schema.Umbraco.Community.Automate.MicrosoftTeams.json` in the package, describing `Umbraco:Automate:MicrosoftTeams`, including `Credentials` as a map of name to `{ WebhookUrl }` with the description "Set per environment as a secret. Never commit it. Names are 1 to 32 letters or digits." and no example values.
  - `pack-release.yml`, Teams deltas only: no nightly contract workflow (Teams has no live tests in CI); the publish step runs only when `github.event.repository.private == false`, so a tag pushed while private builds and attaches artefacts but never pushes to NuGet.
  - **Live gate guard (mock-first, 2026-11-16):** `build/check-live-gate.sh <tag>` runs as the release workflow's first step (whichever file publishes: `publish.yml` per the portfolio CI rules). While `eng/live-gate-open` exists it fails for any tag without `-beta.` and fails if README.md lacks "not yet verified against live Teams". Why a marker file: it's visible in the repo, needs no secret or repo setting, and removing it is one reviewed commit at the Live gate. Local check: `bash build/check-live-gate.sh release/0.0` exits 1 and `bash build/check-live-gate.sh release/0.0-beta.1` exits 0 while the marker exists.

- [ ] **Step 1: Write the failing checks**

```bash
dotnet pack SRC -c Release -p:AutomateMajor=18 -p:VersionSuffix=beta.1 -o artifacts
unzip -l artifacts/Umbraco.Community.Automate.MicrosoftTeams.18.0.0-beta.1.nupkg | grep -E "appsettings-schema|buildTransitive"   # expect 2+ lines
unzip -l artifacts/*.18.0.0-beta.1.nupkg | grep -cE "App_Plugins|wwwroot|\.cshtml"   # expect 0 (S3/P5)
unzip -p artifacts/*.18.0.0-beta.1.nupkg '*.nuspec' | grep -E 'Umbraco.Automate.Core|Umbraco.Cms.Core'   # expect [18.5.1, 19.0.0) and [18.3.0, 19.0.0)
unzip -p artifacts/*.18.0.0-beta.1.nupkg 'appsettings-schema*.json' | grep -c "Credentials"   # expect >= 1
unzip -p artifacts/*.18.0.0-beta.1.nupkg '*.nuspec' | grep -c 'repository.*commit='   # expect 1 (SourceLink)
```

- [ ] **Step 2: Run them, and check the schema checks fail.** Expected: the schema greps print nothing.

- [ ] **Step 3: Implement** the schema class, the buildTransitive props and `umbraco-marketplace.json` (category integrations, tags `automate`, `teams`, `microsoft`; the description's first line: "Uses Teams **Workflows** webhooks. Office 365 connectors were retired in May 2026."). Copy the three Jira files and apply the delta.

- [ ] **Step 4: Re-run Step 1, then the 17 line.** Expected: schema greps 2+ and 1+, client-asset count `0`, nuspec `[18.5.1, 19.0.0)` / `[18.3.0, 19.0.0)`. With `-p:AutomateMajor=17`: `17.0.0-beta.1` and `[17.5.1, 18.0.0)` / `[17.8.0, 18.0.0)`. Then `actionlint .github/workflows/*.yml` (no output); by eye: no `pull_request_target`, every `uses:` SHA-pinned, `id-token: write` only on the publish job; `diff` against Jira's copies shows only the listed delta.

- [ ] **Checkpoint (Pascal commits):** `git add .` / suggested message `build: appsettings schema, packaging checks and release workflow`. After you push, check that the CI run is green on every leg, including the packed-README step. **M3 part 1 done.**

---

### Task 18: README and SECURITY.md

**Files:**
- Create: `SECURITY.md`, `umbraco-marketplace-readme.md`
- Modify: `README.md` (packed as the NuGet readme)

Content follows spec section 10, docs items 1 to 11, plus the section 1 comparison table, using the S9 binding expressions, the S2 Deploy finding and Jira's rotation record. `docs/` gets no user guides (Global Constraints). Owner for the F07, F12, F13 and F15 items: `content-strategist`; `security-analyst` reviews `SECURITY.md` and the honest claims (in Task 19).

`README.md` sections, with these anchors: `#setup`, `#secrets`, `#throttling`, `#telemetry`, `#who-can-post`, `#gdpr`, `#recipe-message-received`, `#migration`. `SECURITY.md`: honest claims, reporting, the rotation runbook (`#rotation`), who else can rotate.

Must-haves:
- First screen of the README: "Workflows webhooks, not Office 365 connectors".
- README: "v17 sites must pin `--version 17.*`". Requirements: Umbraco 17.8.0+ or 18.3.0+, Automate 17.5.1+ or 18.5.1+.
- Setup: the credential name rule ("1 to 32 letters or digits; case doesn't matter") and `Umbraco:Automate:MicrosoftTeams:Credentials:{name}:WebhookUrl`, with Umbraco Cloud secrets, environment variable (`Umbraco__Automate__MicrosoftTeams__Credentials__{name}__WebhookUrl`) and Key Vault examples, and that it needs config access.
- Secrets: "treat the URL like a password"; never in committed appsettings, `Umbraco:Automate:Secrets:*`, HTTP Request steps, tickets or chat; never add the credential section, a parent of it, an empty entry or a wildcard to `AllowedConfigurationKeyPrefixes`; "don't switch on `DisableUriRedaction`".
- Honest claims (spec section 4), word for word in README and `SECURITY.md`, including "Automate section access still means the ability to post" and "credential names are site-wide".
- Rotation runbook: on leak, staff exit and at least every 90 days; per-provider restart note; who else can rotate.
- Ownership: service account, co-owners, one dedicated flow per channel.
- Throttling and the circuit breaker: 4 requests per second, the token bucket, Retry with backoff, one webhook per alerting automation, a digest automation for security alerts, and why data is shortened or removed rather than failing.
- Telemetry: App Insights and OpenTelemetry, with the processor snippet.
- Who can post: one connection per channel, which workspaces to allow it in, a separate workspace for security alerting.
- GDPR/DPIA: Secure inputs and outputs, private channel and guest check, `RunCleanup:RetentionDays`, Microsoft as processor, staff names in built-in templates, the personal-data key warning.
- Migration: none from 0.x; the future HQ scoped-reference path.
- `{{` can't be escaped in v1; a `{{key}}` arriving in bound Title or Message text is filled from Data.
- A `202` means queued, not delivered.
- Until the Live gate passes: "not yet verified against live Teams" near the top of README, in `PackageDescription` and in `umbraco-marketplace.json` (mock-first, 2026-11-16). Content check: `grep -c "not yet verified against live Teams" README.md` expect >= 1.
- Recipe: Automate's built-in Webhook trigger **secured with the plain-secret header authenticator** (pitfall S4), with the secret kept in Power Automate secure inputs; rotate together; Teams message text is untrusted for later steps (AI and publishing steps especially); narrow service-account permissions.
- No `$Umbraco:Automate:Secrets:` example anywhere for the webhook URL.

- [ ] **Step 1: Write the docs**

- [ ] **Step 2: Run the content checks**

```bash
grep -c "not Office 365 connectors" README.md                          # expect >= 1
grep -cE "^## Setup|^## Recipe" README.md                              # expect 2
grep -c "authenticator" README.md                                      # expect >= 1
grep -c "untrusted" README.md                                          # expect >= 1
grep -c -- "--version 17" README.md                                    # expect >= 1
grep -l "MicrosoftTeams:Credentials" README.md SECURITY.md             # expect both
grep -c "1 to 32 letters or digits" README.md                          # expect >= 1
grep -l "site-wide" README.md SECURITY.md                              # expect both
grep -c "90 days" SECURITY.md                                          # expect >= 1
grep -c "DisableUriRedaction" README.md                                # expect >= 2 (secrets and telemetry)
grep -rn "Automate:Secrets:.*Teams\|Secrets:Teams" README.md SECURITY.md    # expect no output
grep -rnE "sig=[A-Za-z0-9%]{6,}" README.md SECURITY.md docs            # expect no output
./scripts/docs-guard.sh; echo $?                                       # expect 0
```

- [ ] **Step 3: Run the `humanizer` skill on README.md and SECURITY.md, then re-run Step 2.** Expected: the same results.

- [ ] **Checkpoint (Pascal commits):** `git add README.md SECURITY.md umbraco-marketplace-readme.md` / suggested message `docs: setup, credentials, security, rotation, telemetry and message-received recipe`. **M3 done.**

---

## M4: Review and release

### Task 19: Final whole-branch review (native build's one independent review)

Portfolio "Build approach": native execution, then one fresh reviewer before any release.

- [ ] **Step 1: Re-check the copy.** Read the contract's status and version. If a newer `credential-helper-*` tag exists, re-copy per contract section 6 (Task 7 steps 2 and 6) before review. Run Task 7 step 2's `diff` again. Expected: no output, and C17 and C18 pass.
- [ ] **Step 2: Dispatch `qa-reviewer`** (fresh context, most capable model) with: the branch diff from the first commit, `docs/design.md`, `docs/plan.md`, `docs/spikes.md`, the contract file and the team's Umbraco pitfalls reference (Critical and Security items are mandatory checks). Ask for findings by severity against the plan's Global Constraints and Review Focus.
- [ ] **Step 3: Dispatch `security-analyst`** (fresh context) with the same inputs plus `security-review.md`: the secret path (store copy, sender, logs, telemetry), the honest claims in README and SECURITY.md, and the CI and release workflows. A Security Critical is never argued down.
- [ ] **Step 4: Fix and re-run.** Fix each finding under its owning task's tests, then `./scripts/test.sh`. Expected: `Failed: 0` per line and green CI. Record the reviews' verdicts and the fixes in `docs/spikes.md` (no secrets, no tenant-specific hosts).
- [ ] **Checkpoint (Pascal commits):** `git add -A` / suggested message `fix: address final review findings`

---

### Task 20: Repository settings, then beta packages

**Pascal does these steps.** Claude only checks the results.

- [ ] **Step 1: Go public**. Do it before the first push that runs Actions.
  - Pre-public check: `ls docs` shows only `design.md plan.md spikes.md`; `git log --all --name-only --format= | sort -u | grep -iE "security-review|plan-review|council|scope\.md"` prints nothing; `./scripts/docs-guard.sh` exits 0.
  - `gh repo edit PascalA94/Umbraco.Community.Automate.MicrosoftTeams --visibility public --accept-visibility-change-consequences`. Expected: `gh repo view --json visibility -q .visibility` prints `PUBLIC`.
  - **Repository settings:** secret scanning with push protection; a branch ruleset on `main` requiring a PR and the checks `build (<line> floor)`, `build (<line> latest)`, `secrets`, `docs-guard`, `osv / osv-scan` and `deps (<line>)`, and blocking force pushes; a tag ruleset so only Pascal creates `release/*` tags; environment `nuget` with Pascal as required reviewer; NuGet trusted publishing for this repo and `pack-release.yml` if available, otherwise a key scoped to push this package ID only, shortest expiry, stored only in the `nuget` environment. **No repository-level `NUGET_API_KEY` secret** (F08): `gh secret list` shows none.
  - Expected: `TeamsDocs.SetupUrl` opens the README's Setup section in a private browser window.
- [ ] **Step 2: Re-check facts.** Latest Automate 17.x/18.x and CMS patches; the CI latest leg is green on them; `AllowedConfigurationKeyPrefixes` defaults unchanged (contract I06 green on latest). If a newer Automate patch exists, confirm the weekly run or a manual run of the full suite is green on it.
- [ ] **Step 3: Tag and push.** `git tag release/0.0-beta.1 && git push origin release/0.0-beta.1`. Approve the publish job in the `nuget` environment. Aliases are now frozen.
- [ ] **Step 4: Verify.** Run: `gh run list --limit 1` and `curl -s https://api.nuget.org/v3-flatcontainer/umbraco.community.automate.microsoftteams/index.json`. Expected: the run succeeded; the index lists `17.0.0-beta.1` (if `eng/lines.txt` has 17) and `18.0.0-beta.1`, with `0.0.1-alpha` still there.

---

### Task 21: Post to the sandbox channel for real, then release

**Moved to the deferred Live gate (mock-first, 2026-11-16).** Run it only as part of the Live gate checklist at the end of this plan, when a tenant exists. Until then the newest beta is the release.

The final acceptance run. It uses the published beta packages, not a local build, on both demo sites. Pascal runs it and the URL stays in his secret store.

- [ ] **Step 1: Install the betas.** On the v17 site (Umbraco 17.8.0+): `dotnet add package Umbraco.Community.Automate.MicrosoftTeams --version 17.0.0-beta.1`. On v18 (Umbraco 18.3.0+): `18.0.0-beta.1`. Set `Umbraco__Automate__MicrosoftTeams__Credentials__Sandbox__WebhookUrl` from your password manager as an environment variable. Also set a second credential `Other` to a second sandbox Workflow.

- [ ] **Step 2: Connection checks (both sites)**
  - Add a connection with credential name `Sandbox`, then press **Test**. Expect the test card in `automate-test`, and "Connected (Teams accepted the message)." plus the honest-claims line.
  - Type the sandbox URL itself into the credential-name field and save. Expect "Credential name must be 1 to 32 letters or digits.", the URL not echoed, and nothing stored. Repeat with `ops-alerts`: same result.
  - Set credential `Retired` to `https://x.webhook.office.com/a`, use it and test. Expect the retired-URL message, and no card.
  - Use a name with no configured value. Expect "No value is configured at Umbraco:Automate:MicrosoftTeams:Credentials:<name>:WebhookUrl."
  - Export an automation and run `grep -c "sig=" export.json`. Expect `0`.
  - Query the database for the connection row (table name from S2). Expect the name `Sandbox` and no URL.
  - **Deploy-transfer the connection** to the second environment, which has its own `Sandbox` value (D11, F14). Grep the transfer artefact and the target database for `sig=`. Expect `0`, and Test on the target posts to the target's channel. **Required for GO**, and closes any S2 Deploy UNKNOWN.
  - Add `Umbraco:Automate:MicrosoftTeams` to `Umbraco:Automate:AllowedConfigurationKeyPrefixes`, restart, and test. Expect the contract's guard message naming that entry, one Critical log line at startup, and no card. Remove it.
  - As a non-admin backoffice user, call the connection create, read and test endpoints for this type. Expect 403 (item 22).

- [ ] **Step 3: Isolation check by hand (council test 1, both sites).** Build an automation with an HTTP Request step whose URL and a sensitive header contain `$Umbraco:Automate:MicrosoftTeams:Credentials:Sandbox:WebhookUrl`, pointing at a local request catcher. Run it. Expect the literal reference text or an error, never the URL, in what the catcher received.

- [ ] **Step 4: Action checks (both sites).** Build automations for use cases 1 to 4 (spec section 3) with the S9 bindings:
  - Post message: one in Plain text (the default) with `[Click](https://evil.example)` and `https://evil.example/login`; one in Markdown with author bold text and a `Data` row holding `[x](https://evil.example)`;
  - each of the four templates, plus Approval needed with `url` bound to `https://evil.example` (expect no button and a warning);
  - one Custom card with `{{missing}}`;
  - a Form submitted card with an extra `email` row (expect the ignored-row warning and no personal-data warning);
  - two Teams connections in one workspace and a step with no connection chosen: expect publish blocked (F06).
  Check the cards on desktop and mobile, and take screenshots. Check that no bare link in data is clickable. Point the connection at a deleted Workflow, run it, and expect "The workflow was deleted or turned off."

- [ ] **Step 5: Circuit breaker check (F01, item 20, both sites).** Submit the demo form 10 times in a row with a 100 KB value in a field bound into a Custom card and into a Post message. Expect 10 posts in the channel, shortened, each run successful with a warning, and the automation still **Healthy** in the circuit-breaker view. Then submit 20 forms in 2 seconds to two automations sharing the `Sandbox` credential and record whether any run fails with `RateLimiting`.

- [ ] **Step 6: Check that nothing leaked.** Run: `grep -rc "sig=" <site>/umbraco/Logs | grep -v ':0$'` and `grep -rc "powerplatform" <site>/umbraco/Logs | grep -v ':0$'`, check the run log in the backoffice, and, if App Insights or OpenTelemetry is on for the demo site, grep the exported telemetry. Expected: no output, no URL or host in any run-log entry. Telemetry findings match the S4 record and the docs.

- [ ] **Step 7: Go or no-go.** **GO** if every check passes on both sites, the Deploy check in Step 2 has run, any UNKNOWN carried from Jira Task 0 is closed, and `security-analyst` has given the release sign-off (built code, this session's `dotnet list package --vulnerable --include-transitive`, the secret-leak tests, and green CI checks). Then Pascal tags `release/0.0` and pushes, approves the publish job, checks both packages on nuget.org, submits to the Umbraco Marketplace, and posts on the forum (optionally also an issue on `umbraco/Umbraco.Automate`). **NO-GO:** fix it under a new task, then repeat Tasks 20 (steps 2 to 4) and 21 with `-beta.2`.

- [ ] **Checkpoint (Pascal commits):** `git add docs/spikes.md` (the results table; screenshots stay out of the repo or go in the PR) / suggested message `docs: 1.0 sandbox acceptance results`

---

## Later (separate plans)

- **P2, Graph:** an OAuth connection, team and channel pickers, posting to chats, reply-to-thread, message IDs. Only if people ask for it on the forum.
- **After 1.0, only if adoption asks:** an opt-in `$`-reference or literal-URL mode, labelled "exposed to F-02", after `security-analyst` review (council condition 10, spec Q2).
- **If HQ ships connection-scoped references:** a documented migration from the package credential section.

## Self-review notes

Re-run 2026-10-30 against the revised spec and the contract.

- **Spec coverage:**
  - 4 (floors, F01 limits, secrets, honest claims): Global Constraints; Tasks 1, 7, 9, 10, 18. 6.1: Tasks 3, 6, 7, 11, 12. 6.2: Task 13. 6.3: Tasks 14 to 16. 6.5: Tasks 3, 13, 16.
  - 7.1 options and build switch: Tasks 1, 6, 17. 7.2 flow: Task 11. 7.3 (including the P9 escape order): Tasks 8, 13, 14. 7.4 (fitter end state): Tasks 9, 13, 16. 7.5 (status and bytes only): Tasks 6, 11. 7.6: Task 6. 7.7 (contract): Task 7, the NO-GO delta. 7.8: Task 10.
  - 8 (M0, 2-day box, stop rule, structural definition, Jira gate): Tasks 3 to 5, Prerequisites P1 and P5. 9: test steps throughout; CI in Task 2; manual in Task 21. 10 (visibility, scanning, docs location, release): Tasks 2, 17, 18, 20; P3, P4, P6. 11: covered by the mitigations' owning tasks.
  - Council tests 1, 2, 3, 6, 7: contract suites in Task 7. Tests 4, 5: Tasks 5 and 21. Test 8: Task 11. Test 10: Task 5 (Jira's record). Condition 7: Tasks 7 and 12. Security review items 1 to 24: items 1 to 4 and 6 in Tasks 6, 11, 12; item 5 became the name rule (Tasks 7, 12), and returns as written in the NO-GO delta; 7 in Task 6; 8 to 10 in Task 11; 11 to 16 in Tasks 8, 13, 14; 17 to 19 in Tasks 9, 10, 13, 16; 20 and 22 in Task 21; 21 in Tasks 13, 16; 23 in Tasks 5, 21; 24 in Task 2 (Semgrep proven here; gitleaks and the vulnerable check proven in Jira).
- **Step scan:** every code step names the file, the signature and the spec values; test steps carry names and assertions. The fitter keeps a short algorithm because its end state is not determined by the signature (P5). No step says "handle edge cases" or "TBD". Task 7's copy steps are commands, because the contract forbids anything but a token replacement.
- **Type consistency:** `TeamsCredentialStore.TryGet(string?, TeamsCredentialPart, out string?, out string?)` in Tasks 7, 11 (only caller); `TryNormaliseName` in Tasks 11, 12; `TeamsCredentialSourceCheck.AppsettingsFileSupplying(string)` in Tasks 7, 12; `RenderOutput` in Tasks 9, 13, 14, 16; `FitResult.Warnings` / `EmptiedKeys` in Tasks 9, 13, 16; `WebhookRateLimiter.WaitAsync(string normalisedCredentialName, ...)` in Tasks 10, 11; `TeamsWebhookSender` constructor with `TimeProvider` in Task 11; `ValidatedWebhookUrl.Host` is internal and used only by `WebhookUrlValidator`. `PackageCredentials`, `CredentialLookup`, `CredentialIsolationGuard`, `TeamsStartupGuardHandler`, `TeamsOptions.CredentialsRoot`, `AutomateCoreVersion` and `AUTOMATE17` appear nowhere in the tasks.
- **Review Focus:** eleven lines, ordered by likelihood, each with an owning test. The skill asks for five; the plan review (P12) asked for four more than the original seven, so all are kept.
- **Proportion:** FsCheck, the 10k-placeholder timing test, the memory assertion, CodeQL, three failing-PR rehearsals and the hand-written CI and release workflows are gone (P14); the user docs shrank from seven pages to two files. Kept: every isolation, guard, leak and injection test. The remaining code blocks are test names and assertions, the copy commands, and the fitter's end state.
- **Spec gaps found while planning, and how they're handled:**
  1. Save-time validation can't warn. Every string blocks the save. So the warnings move to run time (Task 16).
  2. `ConnectionTypeBase` has no `SetupDocsUrl`. The docs link goes in the descriptions instead (Task 12).
  3. `ConnectionValidationResult.Warning` exists on v18. S3 decides whether to use it (Tasks 5 and 12).
  4. Docs links must work for users: the repo is public, and nothing ships before the README and `SECURITY.md` are on `main`.
  5. `scope.md` puts the build switch in M3, but the spec's S7 needs it in M0. The spec wins: Tasks 1 and 3.
  6. Spike findings go into the repo's `docs/spikes.md`. Pascal copies them back to the scoping folder if he wants both to match.
  7. Unmapped non-2xx statuses become `InvalidResponse` (Task 11, architect's call).
  8. `ConfigurationReferenceResolver` is `internal`; the route to it is Jira Task 0 step 7's, inside the verbatim isolation suite.
  9. `CardDataRow` sits in Task 3 because Post message's `Data` rows need it.
  10. The contract's error texts replace the earlier Teams messages for the credential rows (spec 7.5); the old "See SECURITY.md" pointer is gone from those messages, and README covers it.
  11. Contract section 4 says to copy "from the public Jira repo". Teams copies from a local clone at the tag; the tag, hash and `diff` give the same guarantee.

---

## Plan review changes (2026-10-30)

Source: `plan-review.md` (qa-reviewer, 0 blocker, 6 major, 9 minor) and Pascal's final decisions of 2026-10-30 (portfolio README "Plan-review decisions", "Wave-1 fix decisions" and "Build approach"). The spec changes are listed at the end of `design.md`.

**Task renumbering (old → new):** 1 → 1; new CI task → 2; 2 → 3; 3 → 4; 4 → 5; 5 → 6; 5A → 7; 6 → 8; 6A → 9; 7A → 10; 7 → 11; 8 → 12; 9 → 13; 10 → 14; 11 → 15; 12 → 16; 13 → 17 (CI moved to 2); 14 → 18; new final review → 19; 15 → 20; 16 → 21. **19 tasks before, 21 after.**

| ID | Applied? | What changed |
|---|---|---|
| P1 | Applied (Pascal: copy the contract verbatim) | Task 7 rewritten to the contract's copy checklist: `TeamsCredentialStore` (Jira's API shape, `TryNormaliseName`, `MatchesNameRule`, `TryGetExposingPrefix`, `TryGet`, `KeyFor`), `TeamsCredentialPart { WebhookUrl }`, the startup check on `UmbracoApplicationStartedNotification`, name rule `^[a-z0-9]{1,32}\z`, the segment-aware guard, and the two parameterised suites C01 to C18 and I01 to I06 copied verbatim and run in a 5.3 subclass. `PackageCredentials`, `CredentialLookup`, `CredentialIsolationGuard`, `TeamsStartupGuardHandler` and their tests are gone. `SuppliedBy` became `TeamsCredentialSourceCheck`, outside the store, mirroring Jira. Old P5's cross-reference fixed: Jira's gate is Task 0 and the helper is Task 6a. Teams-only name tests for pasted URLs, `$` references and now-invalid legacy names. Build define changed to `AUTOMATE_17` so the verbatim files compile. |
| P2 | Applied | M1 is conditional on Jira Task 0 recorded GO and the contract Frozen (Global Constraints, P5, Task 5 step 1). The "helper written here first" path is deleted. New "NO-GO delta" section: option-2 fields per security review F02, its test items 5 and 6, the Steward's tests, and the voided tasks. The reviewer's "or Teams S2 GO if Jira is still pending" alternative is **not** applied: Pascal's decision makes Jira Task 0 the only gate, and Teams can't copy an unfrozen contract anyway. |
| P3 | Applied | Through P1: the copied I01 to I06 suite runs the built-in HTTP Request action through Automate's real resolver (Jira Task 0 step 7's route), with the package reference in the URL and a sensitive header; I04 is the Secrets control that must leak; I05 widens the allow-list and must leak and trip the guard; I06 checks the referenced Automate's default allow-list. Task 7 step 8 mutation-checks the controls. |
| P4 | Applied | Logs carry the status code and byte count only, never the host (Global Constraints, Task 11 step 3, spec 7.5). `ValidatedWebhookUrl.ToString()` returns a constant; `Host` is internal. Tests: `ValidatedWebhookUrl_ToString_HasNoHostPathOrQuery`, `Post_LogsStatusAndBytesOnly`, `ValidatedWebhookUrl_AsStructuredLogArgument_NoHost`; the sentinel-host test is kept. Task 21 step 6 greps logs for the host too. |
| P5 | Applied | Fitter end state defined (empty-render check, 64 halvings, then emptying longest-first; warnings from the final render, de-duplicated). Tests for 50 rows of 2,000 six-byte-escaped characters, 64-round exhaustion, escape expansion near the limit, de-duplicated warnings, and `Execute_AdversarialData_NeverFailsValidation` on both actions. |
| P6 | Superseded 2026-11-03 | The repo is public at scaffold and visibility no longer depends on a disclosure date. Plan tier stated (GitHub Free). CodeQL replaces Semgrep. gitleaks and CI run from Task 2, straight after scaffold. |
| P7 | Applied | `Post_Timeout_IsTimeoutCategory` uses a never-completing handler and `FakeTimeProvider` through a `TimeProvider`-based timeout. The 10k timing test is replaced by `PlaceholderRegex_HasMatchTimeout`. The 50 MB test asserts bytes read only. |
| P8 | Applied | `Execute_EveryFailure_RunLogAndExceptionFreeOfSentinel_NullInner` in Tasks 13 and 16, over every failure outcome, using the harness context logger (route recorded in Task 3). |
| P9 | Applied | Escape-then-substitute specified as segment-wise escaping (spec 7.3, Task 13 interfaces). Tests: `Build_PlainText_EachCharacterEscapedOnce`, `Build_PlainText_PlaceholderKeyWithUnderscore_Substituted`, `Build_Markdown_DataPlaceholder_EscapedOnce`, `Build_BoundTitleContainingPlaceholder_IsSubstituted_Accepted`; field description and README note. |
| P10 | Applied | `Execute_RateLimiterFull_FailsRateLimiting` on both actions; `TwoActions_SameCredential_ShareBucket`; `Validate_StrictHosts_UnknownHost_Failure_NoRequest`; ignored rows on built-ins are dropped with a warning and no personal-data warning (`Execute_FormSubmittedIgnoredEmailRow_*`); `Execute_CustomCardEmailKey_PersonalDataWarningInOutput`; the conditional F06 fallback test in Task 13. |
| P11 | Applied | The limiter (Task 10) precedes the sender (Task 11). The composer is created in Task 6 and each task registers its own services (renderer in 14, templates in 15). `EditableModelResolverStub` listed in Support and created in Task 3. The two schemas are explained under File structure. |
| P12 | Applied | Review Focus reordered by likelihood with owning tests; added host leak through logs or `ToString`, a full or shared limiter, helper divergence from Jira, and fitter non-convergence; also added the stricter-name-rule case. |
| P13 | Applied | "Structural" defined (more than 3 files with `#if`, different TFMs, or DI, lifetime or composer-API differences). UNKNOWN allowed only for S2 Deploy. Sandbox stop rule in P1 and spec 8. M0 time-boxed at 2 days, with one Pascal extension. |
| P14 | Applied, as Pascal decided | FsCheck dropped (fixed theory of awkward strings instead); 10k-placeholder timing test dropped; Jira's `build-test.yml`, Dependabot, scripts and `pack-release.yml` reused with listed deltas; scan blocking proven in Jira, only Semgrep proven here; CodeQL and the three failing-PR rehearsals dropped; `GuardReleaseVersion` dropped (Jira's derived version can't mismatch); user docs folded into README and SECURITY.md. Re-estimated at 6 to 8 working days. Every isolation, guard, leak and injection test kept. The two schemas are kept (P11 explains why). |
| P15 | Applied, adjusted | A weekly scheduled run of the full matrix (floor and latest per line) that opens an issue on failure (Task 2 deltas 2 and 3). Weekly, not Jira's daily, to save private-repo Actions minutes. |

**Pascal's decisions applied alongside:** native execution with one final review (header, new Task 19); public docs are `design.md`, `plan.md` and `spikes.md` only (Task 1 step 1, docs guard, Task 20 step 1), so spike findings moved from `docs/design.md` section 14 to `docs/spikes.md`; "Checkpoint (Pascal commits)" kept on every task with no AI attribution.

## Final decisions (Pascal, 2026-10-30), superseding the text above

- **S13 partial result:** if isolation passes on 18 but fails on 17, ship 18 with package-owned credentials and defer 17 (matches Jira Task 0).
- **NO-GO fallback:** **no literal webhook URL.** The fallback accepts only a whole-value `$Umbraco:Automate:Secrets:*` reference, per the council's Steward condition. This replaces F02's literal-URL allowance in the NO-GO delta.
- **Jira's plan** has no visibility condition either, so the earlier conflict is resolved.

## Visibility (Pascal, 2026-11-03)

The repo is public at scaffold. Nothing is reported to Umbraco HQ and nothing waits on a reply.

- **The repo goes public at scaffold (Task 1),** like the other packages, with CodeQL, a branch ruleset, gitleaks and the vulnerable-package check. Semgrep isn't needed.
- **Why:** the Automate secret-scoping behaviour is HQ's documented design (PR #71), so describing it needs no embargo.
- **The isolation spike and the package-owned credentials stay exactly as planned.** They're still the right control.
- **The helper copy** comes from the public Jira repo at tag `credential-helper-1.0.2` (the newest tag).
- **Review documents and scope notes** stay outside the repo.
- **Announcements** are posted when the plan says.


## Test tenant (Pascal, 2026-11-03)

**Superseded 2026-11-16:** Pascal can't get the Business Basic trial either. See "Mock-first, live gate deferred" below.

This supersedes "M365 Developer Program sandbox" everywhere above.

- Pascal does **not** qualify for the M365 Developer Program sandbox (checked 2026-11-03).
- The test tenant is a **Microsoft 365 Business Basic free trial** (1 month; card needed; about GBP 4.90 per user per month after the trial). Pascal starts it **on the day the Teams build session begins**, not before, so the month isn't wasted.
- Setup: create a tenant (e.g. `pascalsandbox.onmicrosoft.com`), a team "Umbraco Automate Test" and an "alerts" channel. Create the Workflows webhook ("Post to a channel when a webhook request is received") when the plan needs it.
- The webhook URL is a secret. It goes into an env var under the package-owned prefix (`Umbraco__Automate__MicrosoftTeams__Credentials__sandbox__...`), never into chat or the repo.
- **Cancel the trial** after the final "post to the sandbox channel for real" task.
- Not allowed: Manifesto's tenant, unless IT agrees in writing.

## Mock-first, live gate deferred (Pascal, 2026-11-16), superseding the text above

Pascal can't get a Microsoft 365 tenant: not eligible for the Developer Program sandbox, and not for the Business Basic trial either. The build starts against a mock and is tested live later.

**What changes**
- **Stop rule → deferred.** No tenant is no longer NO-GO. M0 to M3 and Tasks 19 and 20 run against the Task 4a mock.
- **Moved to the Live gate:** Task 4 Steps 3 and 4 (S1, S5) and Task 21.
- **Beta only until the Live gate passes.** Only `-beta.N` tags. README, `PackageDescription` and `umbraco-marketplace.json` say "not yet verified against live Teams" (honest claims). Nothing calls Teams support stable.
- **Release guard:** `eng/live-gate-open` marker file plus `build/check-live-gate.sh` as the release workflow's first step (Task 17). A non-beta tag fails while the file exists. Chosen over a repo variable because it's in the diff, needs no secret or setting, and only a reviewed commit can remove it.
- **One facts file:** `SRC/Configuration/TeamsWorkflowsFacts.cs` holds every Teams-behaviour assumption, tagged **[unverified: from Microsoft docs, not tested live]** there and in `docs/spikes.md`. The Live gate corrects them in one edit.
- **Config, not constants:** card version (`DefaultCardVersion`, `CardVersions`) and host patterns (`AllowedHosts`) are `TeamsOptions` settings with documented defaults from the facts file.
- **Re-verify lines** are on Tasks 6, 8, 9, 10, 11, 12, 15 and 16.

**Unchanged (every security rule stays):** no webhook URL in any file, chat, commit, ticket or CI secret; package-owned credential prefix `Umbraco:Automate:MicrosoftTeams:Credentials`; the name rule `^[a-z0-9]{1,32}\z`; the allow-list fail-closed guard; the credential helper copied verbatim from the Jira repo at the **latest** `credential-helper-*` tag (read what changed since 1.0.0 first). The mock and the demo use only made-up URLs on `.invalid` hosts.

**Risks of mock-first**
- The card format or the webhook's behaviour may differ from Microsoft's docs. Cards, escaping or error handling may need rework at the Live gate.
- A card that passes the Adaptive Card schema can still render badly in Teams, or differently per client (desktop, web, mobile).
- Auto-link breaking (F03) is a security control that only a live client can prove. Until then it's unverified, and `security-analyst` should know.
- Throttling and size limits are guesses until measured, so the limiter and fitter defaults may move.
- **Mitigation:** keep card templates as JSON data files (Task 15 already embeds them); prefer a JSON data file for the Post message card skeleton too. Keep every Teams fact in the one facts file. Then most rework is data edits plus a test run.

### Live gate checklist (run in one session when a tenant exists)

About 2 to 3 h, mostly Pascal. Not counted in the README hours.

Before: a tenant you control (not a client's; not Manifesto's unless IT agrees in writing), a team with a **private** channel `automate-test`, two Workflows from "Post to a channel when a webhook request is received". URLs in the password manager only. Install the newest `-beta.N` on both demo sites.

1. **S1 (Task 4 Steps 3 and 4):** `read -rs TEAMS_SPIKE_URL && export TEAMS_SPIKE_URL`, then run every payload through `spikes/post.sh`. Record: status and body of a good post; whether bare text fails; the host **pattern** (never the tenant host).
2. **S5:** card versions 1.2 to 1.5 on desktop, web and mobile; `summary` vs `fallbackText` in the notification; `width: Full`; backslash escapes honoured or not; each `autolink.json` variant clickable or not, per client.
3. **Facts not covered by S1/S5:** post a body over the size limit, a body with bad JSON, and a burst of 20 posts in 2 seconds; post to a deleted or turned-off Workflow. Record status, body shape and `Retry-After` for each (scrubbed, through `post.sh`).
4. **Correct the facts:** compare each record with `TeamsWorkflowsFacts`. Edit the file once. In `docs/spikes.md` change each tag to **[verified live <date>]**, or record the new value. Run `./scripts/test.sh`. Rework templates or the classifier where a fact changed, under its owning task's tests.
5. **Re-verify Tasks 6, 8, 9, 10, 11, 12, 15 and 16** (their re-verify lines). Any rework ships as the next `-beta.N`.
6. **Task 21 Steps 1 to 6** on both sites with that beta.
7. **Close the gate (Task 21 Step 7):** if GO, delete `eng/live-gate-open`; remove "not yet verified against live Teams" from README, `PackageDescription` and `umbraco-marketplace.json`; `security-analyst` signs off as Task 21 says; then the stable tag. If NO-GO, fix under a new task and repeat from step 4 with the next beta.

**Record in `docs/spikes.md`:** the date; the tenant type (never its name or host); each fact's documented value and live value; the client versions checked; which tasks needed rework; the beta version tested; GO or NO-GO.
