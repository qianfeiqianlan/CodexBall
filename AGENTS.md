# CodexBall Agent Notes

## Project

CodexBall is a Windows-first .NET 9 WPF desktop widget for showing Codex rate-limit status.

## Build And Test

- Build: `dotnet build`
- Test: `dotnet test`
- Run app: `dotnet run --project .\CodexBall.App`

## Architecture

- `CodexBall.App` owns WPF UI, window behavior, local settings, and view models.
- `CodexBall.Core` owns Codex process startup, JSON-RPC, protocol parsing, and formatting helpers.
- `CodexBall.Tests` focuses on core parsing and formatting behavior.

Keep Codex protocol JSON out of WPF code-behind. UI should consume `StatusBallViewModel` and Core models.

## Product Constraints

- Do not ask users for API keys.
- Do not read browser cookies, private token files, or private web APIs.
- Use `codex app-server --stdio` and reuse local Codex login state.
- Keep MVP behavior lightweight: no database, backend service, WebView, or extra frameworks unless explicitly requested.

## UI Notes

- Main widget remains 64 x 64 DIP.
- Preserve transparent, borderless, no-taskbar, always-on-top behavior.
- Dragging should not trigger the details popup.
- Settings live under `%LOCALAPPDATA%\CodexBall\settings.json`.
