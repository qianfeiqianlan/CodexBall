# Changelog

All notable changes to CodexBall will be documented in this file.

The format is based on Keep a Changelog, and this project uses semantic versioning once releases are tagged.

## [Unreleased]

### Added

- Chinese and English UI, with a tray language menu, immediate updates, persisted selection, and OS UI language detection by default.
- Live2D companion preview in the existing WPF app, using Cubism Native 5 R4.1 Core, a community C# adapter, OpenTK offscreen rendering, and transparent WPF bitmap composition.
- Curated local model integration for Koharu, Haruto, Mao, and Mark, with a developer import script for downloaded runtime assets.
- Breathing/blinking, pointer tracking, model motion fades, click reactions, drag sway, and spring return, subject to each model's supplied assets.
- Versioned skin/action contracts and PNG compatibility, optional-action fallback, quota reactions, and Windows WPF/GPU integration tests.
- Pinned SDK setup, third-party notices, and MSIX packaging project assets for local testing.

### Changed

- Unified ring and companion selection in the tray Appearance menu. CodexBall is the ring; character selection automatically enables companion mode.
- Alt+H toggles edge hiding; Alt+C cycles available appearances in menu order and wraps to CodexBall.
- Edge hiding chooses the nearest of all four work-area edges by normalized distance, remembers the visible position, and retains a hover reveal strip.
- Companion usage display is now a thin progress bar without text or an outer border; clicking it opens usage details.
- Companions remain visible when Codex closes; the ring follows Codex activity. Hidden players pause, and switching to the ring releases rendering resources.
- Packaged apps hide startup/self-upgrade controls and skip GitHub update checks.

### Removed

- Widget right-click menus; only the system tray context menu remains.
- Separate companion-mode toggle, focus mode, Haru appearance, user-folder discovery, reload-appearance and open-appearance-folder actions. Unavailable saved appearances fall back to CodexBall; legacy focus settings are ignored.

## [0.2.2] - 2026-09-16

### Added

- Added an inner status ball ring for short-window reset time remaining.
- Added click-to-toggle single-ring and dual-ring status ball modes.

### Changed

- Consolidated widget actions into the system tray menu and removed the status ball context menu.
- Removed the status ball click details popup.

## [0.1.0] - 2026-09-15

### Added

- Floating 64 x 64 WPF status ball for Codex rate-limit visibility.
- Short-window and long-window rate-limit parsing and formatting.
- Details popup with current account/rate-limit state.
- System tray integration with refresh, show, and exit actions.
- Optional launch-at-startup support.
- Edge-hide behavior for the desktop widget.
- Local settings stored in `%LOCALAPPDATA%\CodexBall\settings.json`.
- GitHub Actions build/test workflow.
- Tag-based GitHub Release packaging workflow.
- MIT license, security policy, contribution guide, and project README.

### Security

- CodexBall does not ask for API keys.
- CodexBall does not read browser cookies or private token files directly.
- CodexBall uses `codex app-server --stdio` and reuses local Codex login state through official Codex tooling.
