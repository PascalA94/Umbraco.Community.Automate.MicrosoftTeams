# Security policy

## Reporting a vulnerability

Please **do not** open a public issue for a security problem.

Report it privately through GitHub's private vulnerability reporting: go to the repository's **Security** tab
(https://github.com/PascalA94/Umbraco.Community.Automate.MicrosoftTeams/security) and choose **Report a vulnerability**.

Please include the affected package version, the Umbraco CMS and Automate versions, steps to reproduce or a proof of concept, and the
impact as you understand it. You should get an acknowledgement within 5 working days. Once a fix is released, the
advisory is published with credit to you, unless you would rather not be named.

Out of scope: vulnerabilities in Umbraco CMS, Umbraco Automate or Microsoft Teams themselves. Report those to the vendor.

## Supported versions

| Package version | Umbraco | Supported |
|---|---|---|
| 17.x | 17 (17.8.0 or later) | Yes |
| 18.x | 18 (18.3.0 or later) | Yes |

Security fixes are released as patch versions on each supported line. The package is in beta and **not yet verified against live Teams**.

## What the package claims

The Teams Workflows webhook URL contains a `sig=` signature. Anyone who has the URL can post to the channel, so the package treats it as a password. The claims, and no more:

1. The package posts only to the URL stored under the connection's credential name.
2. Automate authors cannot read the raw URL through Automate: no Automate step can resolve the `Umbraco:Automate:MicrosoftTeams:Credentials` section, while Automate's `AllowedConfigurationKeyPrefixes` does not cover it.
3. **Automate section access to a workspace that allows the connection still means the ability to post to that channel.**
4. **Credential names are site-wide**, not per workspace. Only admins can create connections, so only admins choose which name a connection uses.
5. Other server-side code that reads `IConfiguration` can still read the section. Protect it like any other server secret.

The package also:

- never logs, throws or returns the URL, and scrubs it from any error text;
- stores only the credential name when it is used as intended. It checks the name at **Test connection** and before every send, not at save (Automate gives a package no save hook), so a URL pasted into the name field **is saved on the connection in plaintext**. The package then refuses to use it, with a fixed message that never echoes it, but the stored copy stays until you clear it (see the runbook below);
- refuses a URL on a host outside its allow-list when `StrictHosts` is on, and blocks retired Office 365 connector URLs;
- neutralises Markdown and bare links in bound data by default (not yet verified in Teams clients), and sends buttons only to this site's own hosts unless an author allows more;
- fails a step only for things an author or admin controls: oversized data never fails a step, though floods can still cause throttling.

### Known residuals

- Widening `AllowedConfigurationKeyPrefixes` to cover the credential section exposes the URL to every automation author. The package detects this and refuses its own reads, but it can't prevent the exposure. Values resolved while an entry was exposed stay in run records (90 days by default). Rotate and purge after any exposure. Allow-list changes need a restart.
- Automate's shared HTTP client logs the webhook host and path at Information. The framework redacts the query string, which holds the `sig=`. Application Insights and OpenTelemetry can record the full URL, so strip the query before export (see the [README](README.md#telemetry)). Don't switch on `DisableUriRedaction`.
- The package is **not yet verified against live Teams**. How auto-linking, card rendering and the notification text behave in real clients is unconfirmed.
- The rate limiter is per process.

<a id="rotation"></a>

## Rotation runbook

**Who can rotate:** you do. The package author can't rotate a URL for you. Name at least two people on your side who can rotate each Workflow, and keep that list in your own runbook. A Workflow belongs to its creator and stops working when they leave, so use a shared service account or add co-owners.

Rotate **after a leak, when a staff member who knew the URL leaves, and at least every 90 days.** Workflows URLs don't expire on their own.

1. In Teams, create a new Workflow from the "Send webhook alerts to a channel" template (keep the old one running). This gives a new URL.
2. Update `Umbraco:Automate:MicrosoftTeams:Credentials:{name}:WebhookUrl` in **every** environment: Umbraco Cloud secret, environment variable or Key Vault.
3. Restart if your configuration provider needs it. A JSON file with `reloadOnChange` is read again on the next run. An environment variable needs a restart, because a running process can't see a changed variable. Key Vault and Cloud secrets depend on the provider's refresh setting.
4. Run **Test connection** on every connection that uses the name.
5. Delete the old Workflow, so the old URL stops working.

### After a suspected exposure

1. Rotate as above straight away.
2. Treat the URL as exposed for as long as it was reachable. Export the Automate runs and Umbraco logs from that window to restricted storage, then delete the run records and log entries that hold the resolved value.
3. If the cause was a widened `AllowedConfigurationKeyPrefixes`, put it back and restart.
4. Check the channel for posts you didn't expect.

### URL pasted into the name field

The credential name field is not sensitive, so Automate saves whatever is typed in it, including a pasted webhook URL, and shows it in the backoffice and any history or export of the connection.

1. Clear the field and save, so the connection holds only a name.
2. Delete the connection's history where Automate allows it, and any export or backup that holds the value.
3. Rotate the Workflow URL as above, because the old one was stored in plaintext.
4. Check the channel for posts you didn't expect.
