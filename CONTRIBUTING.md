# Contributing

Thanks for taking the time to improve CodexBall.

## Development Setup

Requirements:

- Windows.
- .NET 9 SDK.
- Codex installed and available from the command line.
- A local Codex login already configured.

Build and test:

```powershell
dotnet build
dotnet test
```

Run the app:

```powershell
dotnet run --project .\CodexBall.App
```

## Project Boundaries

CodexBall should stay small, local, and Windows-first:

- Do not ask users for API keys.
- Do not read browser cookies, private token files, or private web APIs.
- Use `codex app-server --stdio` and reuse local Codex login state.
- Keep Codex protocol details in `CodexBall.Core`.
- Keep WPF UI code consuming view models and core models.
- Keep the main widget at 64 x 64 DIP.

## Pull Requests

Before opening a pull request:

- Run `dotnet test`.
- Keep changes scoped to one feature or fix.
- Include tests when changing parsing, formatting, or shared core behavior.
- Update README, SECURITY, or CHANGELOG when behavior, release packaging, or user-facing boundaries change.

## Commit Style

Use short imperative commit subjects, for example:

```text
Add release workflow
Fix edge hide activation
Document manual push policy
```

When Codex contributes to a commit, include:

```text
Co-authored-by: OpenAI Codex <codex@openai.com>
```

## Push Policy

Do not push commits, branches, or tags on behalf of the maintainer unless explicitly asked. The maintainer normally pushes remote changes manually.

## Security Reports

Please do not disclose sensitive security details in public issues. See `SECURITY.md` for the reporting process.
