# Contributing

Thanks for taking the time to improve CodexBall.

## Development Setup

Requirements:

- Windows x64 and an OpenGL 3.3-capable driver for Live2D tests.
- .NET 9 SDK.
- Codex installed and available from the command line.
- A local Codex login already configured.

Build and test:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Setup-Live2D.ps1
# For the downloaded model ZIPs in tmp/ (PowerShell 7):
pwsh -File .\scripts\Import-Live2DModels.ps1
dotnet build
dotnet test
```

Full GPU tests require imported model assets. For an environment without a suitable GPU, use `dotnet test --filter "Category!=Live2DGraphics"`; this matches CI and does not verify rendering. See [local model integration](docs/imported-live2d-models.md).

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
- Keep the ring at 64 × 64 DIP; companions use their manifest canvas plus a 20-DIP footer.
- Preserve the tray-only menu, unified Appearance selection, Alt+H hiding, and Alt+C appearance cycling.
- Add UI strings through `AppLocalization`; preserve immediate switching and the default `system` preference.
- Keep user model discovery/reload/folder actions closed until explicitly requested.
- See [AGENTS.md](AGENTS.md) for current implementation constraints.

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
