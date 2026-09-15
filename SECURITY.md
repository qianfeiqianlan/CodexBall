# Security Policy

## Supported Versions

CodexBall is currently pre-1.0 software. Security fixes are expected to land on the default branch and in the latest tagged release.

| Version | Supported |
| --- | --- |
| 0.1.x | Yes |

## Reporting A Vulnerability

Please report security issues by emailing `mqyangyang521@qq.com`.

If the issue could expose local account state, tokens, private files, or sensitive machine details, please do not open a public issue with reproduction details. A short private report with the affected version, expected behavior, actual behavior, and reproduction steps is enough to start.

## Security Boundaries

CodexBall is designed to stay local and lightweight:

- It does not ask for API keys.
- It does not read browser cookies.
- It does not read private token files directly.
- It does not call private web APIs.
- It does not attempt to bypass, increase, or alter Codex rate limits.
- It uses `codex app-server --stdio` and reuses the local Codex login state through the official Codex tooling.

Please treat any behavior outside those boundaries as a security bug.
