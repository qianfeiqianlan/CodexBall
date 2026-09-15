# Changelog

All notable changes to CodexBall will be documented in this file.

The format is based on Keep a Changelog, and this project uses semantic versioning once releases are tagged.

## [Unreleased]

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
