# Microsoft Teams for Umbraco Automate

> **Beta: not yet verified against live Teams.** This package is built and tested against hand-built fake responses modelled on Microsoft's documentation, not a real Teams tenant. Only `-beta.N` versions are published until it has been run against a real Teams tenant.

Post messages and Adaptive Cards to a Microsoft Teams channel from Umbraco Automate. It uses Teams **Workflows webhooks, not Office 365 connectors**: connector-based incoming webhooks were retired in May 2026, and Workflows is the only webhook route left.

[![Build](https://github.com/PascalA94/Umbraco.Community.Automate.MicrosoftTeams/actions/workflows/build.yml/badge.svg)](https://github.com/PascalA94/Umbraco.Community.Automate.MicrosoftTeams/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Umbraco.Community.Automate.MicrosoftTeams)](https://www.nuget.org/packages/Umbraco.Community.Automate.MicrosoftTeams)
[![NuGet downloads](https://img.shields.io/nuget/dt/Umbraco.Community.Automate.MicrosoftTeams)](https://www.nuget.org/packages/Umbraco.Community.Automate.MicrosoftTeams)
[![Licence: MIT](https://img.shields.io/badge/licence-MIT-blue)](https://github.com/PascalA94/Umbraco.Community.Automate.MicrosoftTeams/blob/main/LICENSE)

A community package, not made or supported by Umbraco HQ. It is not affiliated with or endorsed by Microsoft. "Microsoft Teams" is a trademark of Microsoft.

<!-- TODO before the first release: add the best screenshot from assets/screenshots/ with an absolute URL, so it also shows on NuGet:
![What it looks like](https://raw.githubusercontent.com/PascalA94/Umbraco.Community.Automate.MicrosoftTeams/main/assets/screenshots/screenshot-01-<what>.png) -->

## Contents

- [Requirements](#requirements)
- [Status and what is verified](#status)
- [Why not the HTTP Request action?](#why-not-the-http-request-action)
- [Install](#install)
- [Setup](#setup)
- [Secrets](#secrets)
- [Actions](#actions)
- [Throttling and the circuit breaker](#throttling)
- [Telemetry](#telemetry)
- [Who can post](#who-can-post)
- [Ownership](#ownership)
- [Personal data (GDPR)](#gdpr)
- [Recipe: message received](#recipe-message-received)
- [Migration](#migration)
- [Configuration reference](#configuration-reference)
- [Limits worth knowing](#limits-worth-knowing)

## Requirements

| Package version | Umbraco | Umbraco Automate |
|---|---|---|
| 17.x | 17.8.0 or later | 17.5.1 or later |
| 18.x | 18.3.0 or later | 18.5.1 or later |

.NET 10. Both lines come from one source tree. **v17 sites must pin `--version 17.*`**, or NuGet picks 18.x and fails to resolve.

<a id="status"></a>

## Status and what is verified

This is a beta. It is **not yet verified against live Teams**: no Microsoft 365 tenant has been available to test with, so every claim about Teams' own behaviour comes from Microsoft's documentation, not from a real channel.

**Covered by fakes, fixtures and tests (unit tests on both Umbraco lines, plus an integration smoke test on SQLite):**

- The sender, against hand-built fake responses modelled on the documented behaviour (a separate mock of the webhook is itself unit-tested, but the sender tests do not run through it): host allow-list, no redirects, size cap, status and error mapping, secret scrubbing and the per-webhook rate limiter.
- The credential store and its guards, against Automate's real configuration resolver.
- Card JSON: the renderer, Markdown escaping, link policy, the payload fitter, and the four built-in templates checked against the Adaptive Card 1.4 schema, and the list of URL-bearing properties checked against the 1.5 schema.
- Publish and save validation, and the Automate action and connection contracts, with Automate's own test harness.
- That a webhook URL never appears in logs, run output or errors from the package's own code, using made-up sentinel URLs that match the documented host pattern.

**Not verified yet, and what the Live gate will check once a tenant exists:**

- That a real Workflows webhook accepts the payload shape, and the host patterns real webhook URLs use.
- How the four cards render on Teams desktop, web and mobile, for card versions 1.2 to 1.5.
- Whether `summary` or `fallbackText` drives the notification text, and whether the link neutralising stops auto-linking in each client.
- The real shapes of error bodies (400, 429, deleted or turned-off Workflow), the real rate limit and the real payload size limit.
- On a running demo site: the backoffice editors, step validation at publish, run-log content, the Form submitted card against Umbraco Forms, Deploy, installing the published prerelease on a clean site, and the telemetry advice below. The only backoffice check so far is a single sign-in and bundle-load smoke test.

Until the Live gate passes, only `-beta.N` versions are published.

## Why not the HTTP Request action?

Automate's HTTP Request action can already post JSON to a Workflows URL. This package exists because it does the job more safely.

| | HTTP Request | This package |
|---|---|---|
| Webhook URL | Pasted into every step, or held in `Umbraco:Automate:Secrets:*`, where any HTTP Request step can resolve it | Held in server configuration under a package-only key, not in the database. The connection holds only a name. No Automate step can resolve the key while `AllowedConfigurationKeyPrefixes` does not cover it |
| Card JSON safety | `${ }` bindings are raw string substitution, so a page called `Rock "n" Roll` breaks the JSON | Data is inserted into a parsed JSON tree and cannot break the JSON |
| Injection | Form data can inject Markdown links | Markdown and bare links in data are neutralised by default (not yet verified in Teams clients). Buttons go only to this site's own hosts unless the author allows more |
| Cards | Write your own | Four ready-made templates |
| Errors | A raw status code | Teams-specific messages, such as "The workflow was deleted or turned off" |
| Size | Teams rejects an oversize message, the step fails, and 10 failures switch the automation off | Long values are shortened to fit, and the post still goes out |
| Throttling | A 429 fails the step | A per-webhook token bucket (3 a second, burst 4, per site process) spaces posts out. A 429 from Teams still maps to `RateLimiting`, and Retry recovers |
| Retired URLs | Silent failure | Old Office 365 connector URLs are blocked with a clear message |

## Install

```bash
dotnet add package Umbraco.Community.Automate.MicrosoftTeams --version 18.*   # Umbraco 18
dotnet add package Umbraco.Community.Automate.MicrosoftTeams --version 17.*   # Umbraco 17
```

While the package is in beta, add `--prerelease`.

<a id="setup"></a>

## Setup

You need **configuration access** to the site (appsettings, environment variables or your host's secret store), not only backoffice access. The webhook URL never goes through the backoffice.

1. **Create a Workflow in Teams.** In the channel, choose **Workflows** and use the template "Send webhook alerts to a channel". Copy the URL it gives you.
2. **Pick a credential name.** 1 to 32 letters or digits; case doesn't matter. For example `Editors`. Names are site-wide.
3. **Store the URL per environment** under `Umbraco:Automate:MicrosoftTeams:Credentials:{name}:WebhookUrl`:
   - **Umbraco Cloud:** add a secret named `Umbraco__Automate__MicrosoftTeams__Credentials__Editors__WebhookUrl` on each environment.
   - **Environment variable:** `Umbraco__Automate__MicrosoftTeams__Credentials__Editors__WebhookUrl`.
   - **Azure Key Vault:** add a secret named `Umbraco--Automate--MicrosoftTeams--Credentials--Editors--WebhookUrl` and load the vault as a configuration source.
   - **Local development:** `dotnet user-secrets set "Umbraco:Automate:MicrosoftTeams:Credentials:Editors:WebhookUrl" "<url>"`.
4. **Add the connection.** In Automate, create a **Microsoft Teams (Workflows webhook)** connection and enter the credential name `Editors`. Enter the name only, never the URL. The name is checked at **Test connection** and before every send, not at save, so a URL pasted into this field **is saved on the connection in plaintext**. If that happens, clear the field, delete the connection's history where you can, and rotate the Workflow URL (see [SECURITY.md](SECURITY.md#rotation)). Test connection and the run both refuse such a name with a fixed message and never echo it.
5. **Test it.** **Test connection** posts a small card to the channel. A bad name, a missing credential, an old Office 365 connector URL and a Workflow that was turned off each give their own message.

Credential names are checked at **Test connection** and before every send, not when you save the connection, because Automate gives a package no hook into the save. Nothing stops a pasted URL being saved, which is why the step above says what to do if it happens.

<a id="secrets"></a>

## Secrets

**Treat the webhook URL like a password.** The `sig=` part of it lets anyone post to the channel.

- Never put it in a committed `appsettings.json`. **Test connection** warns when it finds the URL supplied by an appsettings file.
- Never put it in `Umbraco:Automate:Secrets:*`, in an HTTP Request step, in a ticket or in chat.
- Never add the credential section, a parent of it (`Umbraco`, `Umbraco:Automate`, `Umbraco:Automate:MicrosoftTeams`), an empty entry or a wildcard to Automate's `AllowedConfigurationKeyPrefixes`. If you do, every automation author can send the URL to any host. The package detects this, refuses to resolve credentials and logs a `Critical` message at start-up, but it cannot stop other steps. After any such exposure, rotate the URL and delete the run records and log entries from that time. Automate reads the allow-list once at start-up, so a change needs a restart.
- Don't switch on `DisableUriRedaction`. It stops the framework redacting the query string from logged request URLs.

What the package claims, and no more:

1. It posts only to the URL stored under the connection's credential name.
2. Automate authors cannot read the raw URL through Automate.
3. **Automate section access to a workspace that allows the connection still means the ability to post to that channel.**
4. **Credential names are site-wide**, not per workspace. Only admins create connections, so only admins choose which name a connection uses.
5. Other server-side code that reads `IConfiguration` can still read the section. Protect it like any other server secret.

Rotation is in [SECURITY.md](SECURITY.md#rotation).

## Actions

### Post Teams message (`community.teams.postMessage`)

A simple card with a title, a message, optional data rows and an optional button.

| Field | Notes |
|---|---|
| Title, Message | Bindable. A `{{key}}` in either is filled from **Data**, including a `{{key}}` that arrives in bound text. `{{` can't be escaped in v1 |
| Data | Key and value rows, escaped and shown as facts |
| Link URL, Link label | A button. Only this site's own hosts, unless **Allow external links** is on |
| Style | Default, Good, Warning or Attention |
| Formatting | Plain text (default) or Markdown. Bound data in Title and Message isn't escaped, so put untrusted values in **Data** |

### Post Teams Adaptive Card (`community.teams.postCard`)

| Field | Notes |
|---|---|
| Card | A built-in template, or **Custom** card JSON |
| Card data | Values for the template's `{{key}}` placeholders |
| Summary | Intended as the preview text in the notification (unverified until the Live gate) |
| Escape Markdown in data | On by default. Shows data as typed and stops pasted links being clickable (not yet verified in Teams clients) |

Built-in templates and their keys:

| Template | Keys |
|---|---|
| Content published | `title`, `contentType`, `editor`, `publishedAt`, `url` |
| Approval needed | `title`, `editor`, `notes`, `requestedAt`, `url` |
| Form submitted | `formName`, `entryId`, `submittedAt`, `url` |
| Simple | `title`, `text`, `url`, `linkLabel` |

The templates are plain Adaptive Card 1.4 JSON. Custom card JSON can use `{{key}}` placeholders too. Data is inserted into the parsed JSON, so it can't break it. Full Adaptive Card Templating (`$when`, `$data`) is not supported, and `Action.Submit` doesn't work from a Workflows webhook.

**Form submitted** sends no form field values. It shows the form name and the entry id only. The Form submitted card has not yet been tested against Umbraco Forms.

Notes that apply to both actions:

- **A `202` means queued, not delivered.** Teams accepts the post and the Workflow runs afterwards. A Workflow that fails later doesn't fail the step.
- Data problems never fail a step. A long value is shortened, a bad link is removed, and a warning in the run log names the key, never the value.
- The step fails only for things an author or admin controls: a bad template, a bad connection, a missing credential, or Teams refusing the URL.

<a id="throttling"></a>

## Throttling and the circuit breaker

- Teams answers more than 4 requests a second to one webhook with HTTP 429. A per-webhook token bucket (3 a second, burst 4, per site process) spaces posts out, with a wait of at most 5 seconds. The burst means up to 7 posts can leave in one second, so it reduces 429s rather than ruling them out. Connections that share a credential name share a bucket. The bucket is per process.
- If the wait would be longer than 5 seconds, the step fails with a `RateLimiting` error. A 429 from Teams maps to the same error. Use the step's **Retry** with backoff.
- Automate switches an automation off after 10 consecutive failed runs. That is why oversized data never fails a step: it has its longest values shortened, then emptied, so the post still goes out. Floods can still cause throttling, so a visitor who submits a form many times can slow or drop posts, and repeated failures can still switch an automation off.
- Use **one webhook per alerting automation**, so a busy automation can't starve another. Batch triggers should send one summary, not one post per item. For security alerts, add a scheduled **digest** automation, so a flood still produces one message.

<a id="telemetry"></a>

## Telemetry

The package's own log lines carry the status and size of a post, never the URL. Three things are outside its control:

- Automate's shared HTTP client logs the webhook host and path at Information. The query string, which holds the `sig=`, is redacted by the framework (confirmed for the framework's default HttpClient logging in unit tests, not on a running site). Don't switch on `DisableUriRedaction`.
- Application Insights and OpenTelemetry HttpClient instrumentation can record the full request URL, including the `sig=` query value. Strip the query from any dependency or span whose URL has a `sig=` parameter before telemetry is exported, and check log sinks as well: in Application Insights with a telemetry processor on dependency telemetry, in OpenTelemetry with a filter or by dropping the `url.full` tag. The default `AllowedHosts` (`*.powerplatform.com`, `*.logic.azure.com`, `*.azure-apihub.net`) are not yet confirmed against live Teams, so do not rely on a host list alone.
- This advice has not been tried on a running site, and the package ships no example code for it. After setting it up, post a test message and confirm your exported telemetry holds no `sig=`.

<a id="who-can-post"></a>

## Who can post

- One connection per channel.
- Allow a Teams connection only in workspaces whose members you would trust to post to that channel. Automate section access to such a workspace means the ability to post.
- Use a separate workspace for security alerting.
- Only admins can create connections.

## Ownership

A Workflow belongs to the person who created it and stops working when they leave.

- Create it under a shared service account, or add co-owners.
- Use one dedicated Workflow per channel, so a form flood can't use up the owner's other flows' Power Platform quota.
- Record who owns each Workflow, and who else can rotate its URL (see [SECURITY.md](SECURITY.md#rotation)).

<a id="gdpr"></a>

## Personal data (GDPR)

The Form submitted template sends no personal data. Custom cards, and Post message steps that add form fields, put personal data in three places:

- **Teams.** A channel can include guests or external shared-channel members. Use a private channel and check its membership.
- **Power Automate run history.** Turn on **Secure inputs** and **Secure outputs** on the Workflow's trigger and post steps.
- **Automate run data**, which workspace members can see. Set `Umbraco:Automate:RunCleanup:RetentionDays` to match your data protection impact assessment (DPIA).

List Microsoft (your Power Platform region) as a processor in the DPIA. The built-in templates carry staff names (the `editor` key). Treat any key that holds a name, an email address or form content as personal data.

<a id="recipe-message-received"></a>

## Recipe: message received

The package has no trigger. To start an automation from a Teams message, with no code:

1. In Automate, create an automation with the built-in **Webhook** trigger, **secured with the plain-secret header authenticator**. An unauthenticated webhook trigger lets anyone start the automation.
2. In Teams, build a Workflow: **When a new channel message is added**, then an HTTP POST to the Automate webhook URL with the secret in the header. Power Automate can't easily compute an HMAC, so the plain-secret authenticator is the practical choice. HTTPS keeps the header private.
3. Keep the Automate webhook URL and the secret in Power Automate **secure inputs**. Rotate the secret together with the Workflow.
4. **Teams message text is untrusted input** for every later step, especially AI steps and anything that creates or publishes content. Give the automation's service account the narrowest permissions it needs.

## Migration

None is needed from 0.x. If Umbraco HQ later ships connection-scoped secret references, moving to them means changing the connection to reference the new secret and removing the credential entry. Aliases and settings names in this package are frozen from the first release, so saved automations keep working.

## Configuration reference

All settings are optional. They live under `Umbraco:Automate:MicrosoftTeams`, and the package ships a schema so your editor completes them.

| Setting | Default | Notes |
|---|---|---|
| `Credentials:{name}:WebhookUrl` | none | Set per environment as a secret. Never commit it |
| `AllowedHosts` | `*.powerplatform.com`, `*.logic.azure.com`, `*.azure-apihub.net` | Host patterns a webhook URL may use |
| `StrictHosts` | `false` | When `true`, a host outside `AllowedHosts` blocks the post instead of warning |
| `AllowedLinkHosts` | none | Hosts a card button may link to, besides this site's domains |
| `MaxPayloadBytes` | `28000` | The largest payload sent |
| `MaxValueLength` | `2000` | The longest bound value kept, in characters |
| `TimeoutSeconds` | `15` | Clamped to 1 to 100 |
| `DefaultCardVersion` | `1.4` | The version declared when a card has none |
| `CardVersions` | `1.2` to `1.5` | The versions the connection offers |
| `RateLimit:RequestsPerSecond`, `Burst`, `MaxWaitSeconds` | `3`, `4`, `5` | The per-webhook token bucket |

## Limits worth knowing

- This beta has been tested against fake responses modelled on the documentation, not a real tenant. How the four cards render on desktop, web and mobile, and which field drives the notification text, are unverified.
- The credential name rule runs at **Test connection** and before each send, not at save. A URL pasted into the name field is saved in plaintext until you clear it, so rotate the Workflow URL if that happens.
- The rate limiter is per process. With several nodes the effective rate is the sum.
- `{{` can't be escaped in v1.

## Develop

See `CLAUDE.md` for the commands. In short: `bash build/test.sh` restores, builds and tests both Umbraco lines, and the demo site is under `demo/`.

## Licence

MIT. See `LICENSE`. Security issues: see [SECURITY.md](SECURITY.md).
