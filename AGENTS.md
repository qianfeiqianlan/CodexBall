# CodexBall Agent Notes

## Project

CodexBall is a Windows-first .NET 9 WPF desktop widget for showing Codex rate-limit status, with a ring appearance and curated Live2D desktop companions.

Current product behavior is documented in `README.md` and `docs/desktop-companion-requirements.md`. `design.md` retains the original MVP proposal; its historical roadmap is not a list of implemented features.

## Build And Test

- Prerequisites: Windows x64, .NET 9 SDK; OpenGL 3.3-capable graphics driver for Live2D.
- Before the first build: `powershell -ExecutionPolicy Bypass -File .\scripts\Setup-Live2D.ps1`. This prepares the pinned Cubism Native 5 R4.1 Core DLL and notices. Do not upgrade Core independently of the community adapter.
- Prepare local character assets from the downloaded ZIPs in `tmp/`: `pwsh -File .\scripts\Import-Live2DModels.ps1` (PowerShell 7). Runtime models and downloaded SDK files are Git-ignored; a fresh clone does not contain them.
- Build: `dotnet build`
- Test: `dotnet test`. Full tests include real OpenGL rendering and require the imported models.
- Without a suitable GPU: `dotnet test --filter "Category!=Live2DGraphics"` (also used by CI). This does not validate actual model rendering.
- Run app: `dotnet run --project .\CodexBall.App`

## Git

- Do not proactively push commits, branches, or tags to remote repositories. The user will push remote changes manually unless they explicitly ask the agent to push.
- Before pushing a release tag like `vX.Y.Z`, verify the local application version in `Directory.Build.props` matches the tag without the leading `v`; update it before tagging if needed.

## Architecture

- `CodexBall.App` owns WPF UI, window behavior, local settings, and view models.
- `CodexBall.Core` owns Codex process startup, JSON-RPC, protocol parsing, and formatting helpers.
- `CodexBall.Tests` covers core parsing, formatting, companion contracts, quota reactions, and edge-hide geometry.
- `CodexBall.App.Tests` covers WPF/player integration, appearance switching, localization, and GPU rendering.
- `CodexBall.App/Companion` owns the curated catalog and PNG/Live2D players behind `ICompanionPlayer`; behavior requests semantic actions rather than asset filenames.
- `AppLocalization` owns UI translations and language resolution; Core formatters accept an explicit language flag. Keep language selection out of Codex protocol parsing.

Keep Codex protocol JSON out of WPF code-behind. UI should consume `StatusBallViewModel` and Core models.

## Product Constraints

- Do not ask users for API keys.
- Do not read browser cookies, private token files, or private web APIs.
- Use `codex app-server --stdio` and reuse local Codex login state.
- Keep MVP behavior lightweight: no database, backend service, WebView, or extra frameworks unless explicitly requested.

## UI Notes

- Ball mode remains 64 x 64 DIP. Companion mode uses a skin-defined canvas (currently 240 x 260 or 240 x 320 DIP, plus a 20-DIP footer), as authorized in the desktop companion requirements.
- Preserve transparent, borderless, no-taskbar behavior; always-on-top remains a saved user option.
- Only the system tray icon has a right-click menu. Do not restore widget context menus or focus mode.
- One Appearance menu combines ring/companion selection. Order: `codexball`, `local-koharu`, `local-haruto`, `local-mao_pro`, `local-mark_free_t04`; unavailable packs are omitted. Choosing a character enters companion mode. Haru and unavailable saved selections fall back to CodexBall.
- Alt+H toggles edge hiding; Alt+C cycles appearances in menu order with wraparound. Register shortcuts even when the ring starts hidden.
- Hide toward the nearest of all four work-area edges using normalized horizontal/vertical gap percentages; restore the previous visible position and retain the hover reveal strip.
- Companion usage display is only a thin progress bar below the character, without text or an outer border; clicking it opens usage details. The ring supports click-to-toggle single/dual rings.
- Companions remain visible when Codex closes; the ring follows Codex activity. Hidden companions pause animation; switching to the ring releases the player.
- Expose only curated bundled models. Do not restore user-folder discovery, reload, or open-folder menu entries. PNG support remains internal compatibility infrastructure.
- Languages: `system` (default), `zh-CN`, `en-US`. Resolve system language from startup `CurrentUICulture`: any `zh` uses Chinese, otherwise English. Apply changes immediately and persist them; new UI strings belong in `AppLocalization`.
- In packaged builds, keep startup/self-upgrade controls hidden and skip GitHub update checks.
- Settings live under `%LOCALAPPDATA%\CodexBall\settings.json`.
