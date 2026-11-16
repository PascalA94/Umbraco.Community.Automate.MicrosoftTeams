# Microsoft Teams for Umbraco Automate

Uses Teams **Workflows** webhooks. Office 365 connectors were retired in May 2026.

**Beta: not yet verified against live Teams.** It is tested against fake responses modelled on the documentation, not a real tenant. See "Status and what is verified" in the full documentation.

Post messages and Adaptive Cards to a Teams channel from Umbraco Automate.

- **Post Teams message** and **Post Teams Adaptive Card** actions, with four ready-made card templates: Content published, Approval needed, Form submitted and Simple.
- The webhook URL stays in server configuration under a name. Automations hold only the name, and no Automate step can read the URL while `AllowedConfigurationKeyPrefixes` does not cover the section. Enter the name only: a URL pasted into the name field is saved on the connection, so rotate it if that happens.
- Data can't break the card JSON, and links in data are neutralised by default (not yet verified in Teams clients).
- Long values are shortened instead of failing the step, and a per-webhook token bucket spaces posts out, and a 429 from Teams is retried by the step's Retry setting.
- Teams-specific error messages that work with Automate's step Retry.

Requires Umbraco 17.8+ with Automate 17.5.1+, or Umbraco 18.3+ with Automate 18.5.1+.

A community package, not made or supported by Umbraco HQ or Microsoft. Full documentation: https://github.com/PascalA94/Umbraco.Community.Automate.MicrosoftTeams#readme
