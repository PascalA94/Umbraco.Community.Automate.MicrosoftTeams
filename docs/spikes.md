# Spike findings

Findings from the M0 spikes in `design.md` section 8. This file records host patterns only, never a tenant-specific host, a webhook URL or a `sig=` value.

- **Repo visibility:** public at scaffold (the disclosure rule was dropped on 2026-11-03).
- **Credential helper:** copied from the Jira repo at tag `credential-helper-1.0.2`, the newest `credential-helper-*` tag. It supersedes 1.0.0 and 1.0.1 (test-only patches: I05 accepts either Automate error shape, I07 pins IL member references, new I05b and I05c).
- **Tags used below:** [VERIFIED] = run or read this session. [INFERRED] = read from source, not run on a site. [PENDING] = needs Pascal (tenant, demo site or Deploy) and has not been run.
- **Source read:** `umbraco/Umbraco.Automate` branches `v17/dev` and `v18/dev`, 2026-11-16. The public API of `Umbraco.Automate.Core` 17.5.1 and 18.5.1 was also diffed from the shipped XML docs.

## M0 gate

| Gate | Spikes | Result | Date |
|---|---|---|---|
| A: one-branch build | S6, S7 | **GO on the build**, no `#if` needed. Steps 5 and 6 of Task 3 (demo-site install) PENDING | 2026-11-16 |
| B: live Teams behaviour | S1, S5 | **DEFERRED (mock-first, Pascal 2026-11-16).** No Microsoft 365 tenant exists. Task 4a is done: facts file and mock. The live posts move to the Live gate | 2026-11-16 |
| C: Umbraco-side behaviour and the Jira gate | S2, S3, S4, S8, S9, S10 | **PARTIAL**: S2 Jira gate, S6, S8, S10 answered from source; S3, S4, S9 and the S2 Deploy check need a demo site | |

M1 may start on the mock once Gate C's demo-site checks are done and Pascal has reviewed this file. Releases are `-beta.N` only until the Live gate passes (`eng/live-gate-open`).

## S1: post to a Workflows webhook

**DEFERRED (mock-first).** No tenant. The assumptions are in `TeamsWorkflowsFacts` and the table under S5. At the Live gate: run `spikes/post.sh` on the payloads, record the status and body of a good post, whether bare `{"text":"..."}` fails, and the host **pattern** (never the tenant host).

## S2: Jira gate, Deploy and rotation

**Jira gate (Jira Task 0, recorded 2026-11-04) [VERIFIED by reading Jira's `docs/spikes.md`]**

- Row: "Steps 3 and 4 pass on both, step 5 or 6 UNKNOWN". **GO on both lines (17 and 18)** with package-owned credentials. Teams has the same residual. `eng/lines.txt` stays `17` and `18`.
- The contract is **Frozen**. Teams copies the newest tag, `credential-helper-1.0.2`.
- Jira's re-check on Automate 17.6.0 and 18.6.0 (2026-11-12) is in its own spikes file; the copied suite runs against the newest Automate in CI on the latest leg.
- Isolation (I01 to I06 and the I07 pin): the copied suite passes for the Teams prefix on both lines at the floor (Teams run, 2026-11-16, 64 tests on 17.5.1 and on 18.5.1 including C17 hash and C18 version). The latest leg (`AutomateFloat=true`, Cms 17.8.1 and 18.3.1) was also run locally on **Automate 17.6.0 and 18.6.0**: 64 of 64 tests passed on each, so the isolation suite holds on the newest Automate. CI has not run yet.

**Rotation (Jira Task 0 step 5, per provider)**

| Provider | Result |
|---|---|
| `appsettings.Development.json` (`reloadOnChange`) | New value read on the next run, no restart |
| Environment variable | Needs a restart, by construction (a running process cannot see a changed variable). Not run |

**Other surfaces (Jira Task 0 step 7, council test 10)**

A scan of the Management and Automate APIs on both lines (17: 4,412 calls; 18: 4,829 calls) plus logs and the SQLite files found the sentinel 0 times. A positive control showed the scanner can see a stored leak. Limits: the backoffice UI was not opened, POST endpoints were not called, and list endpoints were read at the default page size.

**Residual (same text as Jira, for README and SECURITY.md)**

- Widening `AllowedConfigurationKeyPrefixes` to cover the credential prefix exposes the credential to every automation author.
- The guard detects and refuses the package's own reads. It does not prevent the exposure.
- Values resolved while an entry was exposed stay in run records (default 90 days). Rotate and purge after any exposure.
- "No Automate step can resolve the Credentials section" holds only while the allow-list does not cover that prefix.
- Allow-list changes need a restart.

**Umbraco Cloud and Deploy [INFERRED from Jira's record, not run for Teams]**

- Cloud accepted a secret named `Umbraco__Automate__Jira__Credentials__test__ApiToken`, so it does not refuse the package's key shape. Each Cloud environment resolves its own value.
- `Umbraco.Deploy.Automate` transfers connections. `TeamsWorkflowConnectionSettings` holds only `CredentialName` (`IsSensitive = false`) and `DefaultCardVersion`, so only a name travels.
- **Deploy: UNKNOWN.** The Teams-specific transfer check (export grep, Deploy artefact grep, the target resolving its own value) has not run. It is the only spike allowed to stay UNKNOWN (plan review P13). It blocks GO in Task 21, not M1.

## S3: connection save and validation

- **Save hook [VERIFIED by compiling against 17.5.1 and 18.5.1, 2026-11-16: NOT USABLE]:** `ConnectionSavingNotification` exposes its connection only through the protected `ObjectNotification<Connection>.Target`, and the richer `IConnectionSettingsSaveHandler` (whose exception message "is shown to the backoffice user") and `ConnectionSettingsSaveContext` are `internal`. A package can't read the saved connection in either. **Decision (Task 5 step 8 default):** no saving handler is built; the name rule runs at Test connection and before every send, and the README says so. Earlier inference, kept for the record: `ConnectionService` publishes `ConnectionSavingNotification` through `PublishCancelable` on create and update. If a handler cancels, it throws `OperationCanceledException("Connection creation was cancelled by a notification handler.")`. So a handler can block a save. **Unknown:** whether the backoffice shows our handler's message or only that generic text. [PENDING] check on a demo site. If only the generic text shows, the name rule also runs at Test and before every send, and the README says so.
- **`ConnectionValidationResult.Warning` [VERIFIED present on 17.5.1 and 18.5.1]:** `ConnectionValidationStatus` has `Success`, `Warning` and `Failure` on both lines. The plan's note that `Warning` exists only on v18 is wrong. **Unknown:** whether the backoffice shows a Warning from Test connection as a pass with a warning or as a failure. [PENDING] demo site. Default if it shows as a failure: return `Success` with the warning appended to the text.

## S4: framework logging and telemetry

**Partial result (M1, 2026-11-16, unit tests):** Automate's `Constants.HttpClients.NoRedirect` is the client the package uses, because `SsrfProtectionHandler` is internal and a package client can't carry it. With Trace logging on a real `IHttpClientFactory`, the framework's own handlers redact the query (`sig=` never appears), but they **do log the host and path at Information**. That contradicts "logs never carry the host" for framework lines (the package's own lines carry status and bytes only). The `DiagnosticListener` payload still holds the full request (known residual R2, pinned by a test). Decision for Pascal: accept (the host and path are not the signature), or add a package client with `RemoveAllLoggers()` and an equivalent SSRF check of its own. The demo-site run below still has to cover Application Insights and OpenTelemetry.

[PENDING] Needs the v18 demo site with Serilog at `Debug`, Application Insights and OpenTelemetry HttpClient instrumentation on, then `grep -r SPIKESIG umbraco/Logs` and the exported telemetry (Task 5 step 4). Superseded: M1 kept Automate's NoRedirect client (see the partial result above), so the framework logs the host and path; the S4 decision is deferred.

## S5: card versions, escaping and auto-linking

**DEFERRED (mock-first).** The 39 payloads in `spikes/payloads/` and the four draft templates (version 1.4, `{{key}}` placeholders, in `src/Umbraco.Community.Automate.MicrosoftTeams/Cards/Templates/`) are ready for the Live gate. At the Live gate record: which versions render on desktop, web and mobile; whether `summary` or `fallbackText` drives the notification; `width: Full`; whether backslash escapes are honoured; and, per client, whether each `autolink*.json` variant is clickable. Until then the auto-link breaking (F03) is **unproven**: `security-analyst` should be told.

### Facts table (Task 4a): every Teams-behaviour assumption, one row per `TeamsWorkflowsFacts` member

All rows are **[unverified: from Microsoft docs, not tested live]**. Doc links are pointers, to be re-checked at the Live gate.

| Fact | Assumed value | Pointer |
|---|---|---|
| `AcceptedStatusCode` | 202, empty body | support.microsoft.com, "Create incoming webhooks with Workflows" |
| `EnvelopeType` | `message` | learn.microsoft.com, "connectors-using" |
| `AdaptiveCardContentType` | `application/vnd.microsoft.card.adaptive` | same |
| `DefaultCardVersion` | `1.4` | learn.microsoft.com, "cards-reference" |
| `CardVersions` | `1.2`, `1.3`, `1.4`, `1.5` | same |
| `DefaultAllowedHosts` | `*.powerplatform.com`, `*.logic.azure.com`, `*.azure-apihub.net` | S1 at the Live gate |
| `MaxPayloadBytes` | 28000 | "connectors-using" |
| `ThrottleStatusCode`, `ThrottleRetryAfterSeconds` | 429, `Retry-After` of 2 s | "connectors-using" |
| `RequestsPerSecond`, `Burst`, `MaxWaitSeconds` | 3, 4, 5 (a package choice under the documented rate) | none |
| `BadJsonStatusCode`, `BadJsonBody` | 400, a short text | none (real shape unknown) |
| `OversizeStatusCode`, `OversizeBody` | 413, a short text | none (real shape unknown) |
| `WorkflowGoneStatusCode`, `WorkflowGoneBody` | 404, a short text | none (real shape unknown) |

### Card assumptions added in M2 (Tasks 14 to 16), all **[unverified: from Microsoft docs, not tested live]**

- **Buttons are card-level `actions`, not an `ActionSet`.** The four built-in templates use only `TextBlock`, `FactSet` and `Action.OpenUrl` (design 6.3), so the button sits in the card's own `actions` array. Re-check at the Live gate that it renders on desktop, web and mobile, and whether `msteams.width: Full` is wanted.
- **The built-in templates are valid Adaptive Card 1.4** (checked against the pinned 1.4 schema in the tests). Whether each client draws them well is only known live. The pinned schemas came from the `microsoft/AdaptiveCards` repository (1.4.0 and 1.5.0), because `adaptivecards.io` serves only the unversioned latest schema.
- **Auto-link breaking (F03)** still rests on a zero-width space after `:` in `://`, after `www.` and before `@`. Whether Teams turns the broken text into a link anyway is unverified.
- **An empty optional fact still shows as a row** (for example "Editor:" with no value). Teams may drop or keep it. If it looks bad live, prune empty facts in `CardRenderer`.
- **`summary` and `fallbackText` are both set** by `CardEnvelope.ApplySummary`, because which one Teams uses for the notification preview is the open S5 question.

`TeamsOptions` takes its defaults from these members, and the mock (`src/Umbraco.Community.Automate.MicrosoftTeams.Mock`, never packed) answers from them, so a correction is one edit plus a test run.

## S6: outcomes PRs and the test harness

- **PRs [VERIFIED via `gh`]:** umbraco/Umbraco.Automate #445 (`v18/dev`) and #449 (`v17/dev`), "feat(action): Let actions declare named outcome exits", were both **merged on 2026-11-04**. Outcomes are in 17.6.0 and 18.6.0.
- **Harness [VERIFIED]:** `ActionTestHarness.For<T>().WithService<IEditableModelResolver>(..).WithSettings(..).WithConnection(..).ExecuteAsync()` works on both lines. Tests register an explicit `EditableModelResolverStub`, so they pass before and after the outcomes change. The harness does not resolve bindings, so tests pass already-bound settings.
- **Result type [VERIFIED]:** `ActionResult.Status` is an `ActionResultStatus` (`Success`, `Failed`, `Skipped`, `WaitingForInput`, `Sleeping`). There is no `IsSuccess`. `ActionBase<,>` has its own `Success(output)`.
- **Context logger [INFERRED]:** the run-log route is `ActionContext.Log/LogDebug/LogInfo/LogWarning/LogError` with `LogEntries` readable afterwards. It is not the application log. Tasks 13 and 16 capture it through the context after `ExecuteAsync` returns. **Caveat to confirm in Task 13:** the harness may not expose the context it built, so the tests may need to construct an `ActionContext` directly.

## S7: one-branch build on Automate 17 and 18

[VERIFIED] 2026-11-16.

- **Result: GO.** The connection and Post message skeletons compile and the skeleton test passes on **both** lines from one source tree, with **zero `#if AUTOMATE_17` blocks**. `bash build/test.sh` gave 64 unit and 2 integration tests passing on 17 and on 18. Both lines target `net10.0`, so one test project covers both.
- **API comparison (Task 3 step 3):**

| Member | 17.5.1 vs 18.5.1 |
|---|---|
| `ConnectionTypeBase<T>` constructor and `ValidateAsync` | same |
| `ConnectionValidationResult.Success/Warning/Failure` | same |
| `ActionContext.GetSettings<T>()` and `Connection.GetSettings<T>()` | same |
| `ActionResult.Failed(Exception, StepRunErrorCategory)` | same |
| `IValidatableStepType.ValidateSettingsAsync` and `IPublishValidatableStepType.ValidateSettingsForPublishAsync` | same |
| `AutomateOptions.AllowedConfigurationKeyPrefixes` | same (the copied suites compile and pass) |
| `UmbracoApplicationStartedNotification` | same (the helper's startup check compiles and runs) |
| `ConnectionSavingNotification`, `ConnectionService`, `ConnectionValidationResult`, `ActionContext`, `CircuitBreakerService` source | byte-identical on `v17/dev` and `v18/dev` |

  A full diff of the shipped XML docs for `Umbraco.Automate.Core` 17.5.1 against 18.5.1 shows 2 members only in 17 and 8 only in 18. None of the 10 is in the API above. All are constructors of the built-in `GetContentAction` and `GetMediaAction`, which Teams does not use.
- **F06 publish validation route [INFERRED]:** `ValidateSettingsForPublishAsync(object settings, Automation automation, CancellationToken)` receives the whole `Automation`. `Automation.Steps` is a list of `StepConfiguration`, which has `ConnectionId` (a `Guid?`) and `ActionAlias`. (`Automation.Connections` is the step graph's edges, not Automate connections.) `Workspace.AllowedConnections` (an `IList<Guid>`, reached through `IWorkspaceService` with `Automation.WorkspaceId`) lists the workspace's connections, and `IConnectionService` resolves a connection's type. All of this is read from source on `v17/dev` and `v18/dev`. The route is possible, so spec section 8's fallback is not needed. Task 13 confirms the call chain when it writes the check.
- **`CardDataRow` decision:** Teams keeps its own `CardDataRow { Key, Value }`, the same shape as HQ's `HttpRequestKeyValue`. [PENDING] confirm on a demo site that the key/value editor renders and round-trips it (Task 3 step 5). If it does not, switch to HQ's `HttpRequestKeyValue`.
- **Not yet done:** Task 3 step 5, installing the spike packs into both demo sites (Pascal).

## S8: run-log warnings

[INFERRED from source, identical on both lines.] An action can add entries to the step's run log through `ActionContext.LogWarning(string)`. They are persisted with the step run and shown in the run detail UI (`MinimumLogLevel` defaults to `Info`, `MaxLogEntries` to 200). A successful step can therefore show a warning. **Decision:** Teams returns the warnings in `TeamsPostOutput.Warnings` and also writes each through `context.LogWarning`, naming keys only, never values. The `ILogger` warning is no longer needed for visibility. [PENDING] confirm the UI shows them on a demo site.

## S9: bindings

[PENDING] Needs a demo site with `Umbraco.Automate.Forms`. Known from Jira's record (Task 4, 2026-11-04): the Forms trigger alias is `umbracoForms.formSubmitted`, with output `formId`, `formName`, `recordUniqueId`, `state`, `created`, `ip`, `memberKey`, `culture`, `recordFieldsJson` and `fields[]`; the add-on is `Umbraco.Forms.Automate` (17.0.3 and 18.0.3). Still to record: the binding expressions for content name, public URL, backoffice edit URL and editor name. If Forms cannot be tested, the Form submitted docs example is marked "not yet tested against Forms".

## S10: circuit breaker

[INFERRED from `CircuitBreakerService.cs`, identical on `v17/dev` and `v18/dev`.]

- **A step that fails and then succeeds on Retry:** the breaker counts **runs**, not steps. `EvaluateAsync(run)` reads `run.Status`: `Completed` and `Rejected` are successes, `Failed` is a failure, and `Suspended` and `Cancelled` are ignored. A step whose retry succeeds leaves the run `Completed`, so it does **not** count as a failure. A run that exhausts its retries and ends `Failed` does.
- **Rate-limited runs:** the breaker sees only runs that reach a terminal status. Whether Core's own run rate limit produces a run that the breaker counts [PENDING, not in the files read]. A Teams `RateLimiting` failure that ends the run as `Failed` does count, and Retry can hide it by finishing `Completed`.
- **Thresholds:** `ConsecutiveFailureThreshold` disables the automation. A warning comes first at `ConsecutiveWarningThreshold`. Error-rate rules need a full window. Defaults to read from `CircuitBreakerOptions` when Task 21 runs the 10-submission check.
- **Consequence for Q3:** because Retry hides a transient `RateLimiting` failure from the breaker when the retry succeeds, the throttling docs should say a persistent shortage still trips it. [PENDING] the 10 oversized form submissions on a demo site (Task 21).

## Final review (Task 19, 2026-11-16)

Two fresh-context reviews of the whole branch (QA and security). No Critical finding. Fixed and tested (suite 524 unit and 2 integration per line, 0 failures):

- A bound Summary now goes through the payload fitter (reserved key), with control and bidi characters stripped and a 250 character cap, so oversized or escape-heavy data cannot fail a step.
- A webhook URL pasted into the credential-name field is saved before any check can run (no save hook, S3). The docs now say so. Test and send return a fixed message that tells the admin to clear the field and rotate, and never echo the value.
- A link longer than 2,048 characters is dropped with a fixed warning instead of failing validation.
- The 400-body scrubber also covers JSON-escaped URLs, percent-encoded `sig`, bare allow-listed hosts and the validated host.
- The longest-first fitter test now fails if the order is wrong (mutation checked).
- README wording on the token bucket and link neutralising; the osv-scanner job reads the lock files and ignores two false positives with an expiry.

Deferred (not fixed): the S4 framework-logging decision (Automate's shared HTTP client logs the webhook host and path at Information), and these Low items: site content-tree domains join the link allow-list; the `IdnMapping` allow-list comment; the rate limiter test uses a real 2 ms delay; two IL architecture tests to replace dropped static-analysis rules; `check-live-gate.sh` checks only the README for the not-verified phrase; the fitter can halve a URL-valued key; Post message `Data` keys are uncapped; workflow hygiene (`persist-credentials: false`, `security-events` permission, gitleaks range on schedule, pin the mssql image); no `--locked-mode` in `build/test.sh`; no test resolves Automate's real NoRedirect registration. There is no SAST job beyond CodeQL default setup.

## Decisions and open items for Pascal

1. Run Task 3 step 5 (install both spike packs into the demo sites) and Task 5 steps 2 to 4 and 6 (Deploy, backoffice save and warning display, logging and telemetry, bindings). The live posts (Task 4 steps 3 and 4) wait for the Live gate.
2. No Microsoft 365 tenant exists, so Prerequisite P1 is deferred to the Live gate (checklist at the end of `docs/plan.md`).
3. Before M1: Pascal reviews this file.
