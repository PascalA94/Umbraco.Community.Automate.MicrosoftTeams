# Contributing

Issues and pull requests are welcome. For anything bigger than a small fix, open an issue first so we can agree the approach.

## Branches

- `main` builds both Umbraco lines (package 17.x for Umbraco 17, 18.x for Umbraco 18); `-p:AutomateMajor=17` or `18` picks the line.
- Open pull requests against `main`. A change must build and pass the tests on both lines.

## Build and test


```bash
dotnet restore src/Umbraco.Community.Automate.MicrosoftTeams.slnx --locked-mode
dotnet build src/Umbraco.Community.Automate.MicrosoftTeams.slnx -c Release --no-restore
dotnet test src/Umbraco.Community.Automate.MicrosoftTeams.slnx -c Release --no-build
```

That builds the Umbraco 18 line. `bash build/test.sh` restores, builds and tests every line (`bash build/test.sh 17` for one); the
`-p:AutomateMajor=17` property picks the Umbraco 17 line in the commands above.

## Try it on the demo site

`demo/` is an Umbraco site for trying the package. It runs on HTTPS only:

```bash
dotnet user-secrets set "Umbraco:CMS:Unattended:UnattendedUserPassword" "<pw>" --project demo/Umbraco.Community.Automate.MicrosoftTeams.DemoSite
dotnet dev-certs https
dotnet run --project demo/Umbraco.Community.Automate.MicrosoftTeams.DemoSite   # https://localhost:44331/umbraco
```

## Try it in your own site

Pack with a unique version each time, because NuGet's global cache keeps serving the first build of any version number:

```bash
dotnet pack src/Umbraco.Community.Automate.MicrosoftTeams -c Release -o ./local-nuget -p:Version=18.0.0-local.$(date +%s)
dotnet nuget add source ./local-nuget --name local   # once
dotnet add package Umbraco.Community.Automate.MicrosoftTeams --version 18.0.0-local.<timestamp>
```

## Before you open a pull request

- Add or update tests for what you changed, and run them.
- Keep personal data out of logs.
- Security issues go through private reporting (the Security tab), never a public issue or PR.
