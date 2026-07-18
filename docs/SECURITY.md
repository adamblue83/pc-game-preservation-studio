# Security & Privacy

## Commitments

- Never collect platform passwords, session cookies, or authentication
  tokens.
- Never copy full user profiles, browser data, or unrelated personal files.
- Never upload archive information anywhere — the app works entirely
  locally.
- Never execute a game's files during scanning or analysis.
- Never disable antivirus, modify protected executables, or patch DRM.
- Never download or install third-party tools silently; the user always
  triggers installs/downloads explicitly.
- Never delete source game files, or overwrite an existing archive without
  confirmation.
- Never claim a backup is complete before hashing finishes, or that a disc
  is verified before an independent read-back verification finishes.

## Process execution

Any invocation of an external tool (e.g. a future `oscdimg.exe` backend)
uses `ProcessStartInfo.ArgumentList` with validated, non-shell-interpreted
arguments — never string-concatenated shell commands — to avoid injection
risk from user- or filesystem-controlled path components.

## Logging

Structured logs redact usernames in profile paths where practical, and never
record account identifiers not needed for restoration, tokens, credentials,
or other personal data. A user-facing "View Log" button is planned.

## Reporting a vulnerability

This project does not yet have a dedicated security contact process. Until
one exists, please open a GitHub issue marked clearly as a security concern,
or contact a maintainer directly rather than disclosing exploit details
publicly.
