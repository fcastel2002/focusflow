# FocusAnchor

FocusAnchor is a local-first Windows desktop app for choosing, protecting and
reviewing focus sessions. It keeps the scope intentionally small: focus blocks,
brief distraction capture, session reviews and simple calendar planning.

The app does not generate micro-goals, productivity scores or AI advice.

## Requirements

- Windows
- .NET 10 SDK

## Run

```powershell
dotnet build
dotnet test
dotnet run --project src/FocusAnchor.App
```

## Local Data

FocusAnchor stores its SQLite database at:

```text
%LOCALAPPDATA%\FocusAnchor\focus-anchor.db
```

For development or smoke tests, set `FOCUSANCHOR_DATA_PATH` to use an isolated
directory. Themes, rain volume, reviewed sessions, calendars, daily intentions
and planned focus blocks are all stored locally.

The rain player uses a bundled CC0 sound and never starts automatically. See
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for its source and license.

## Google Calendar

Google Calendar is optional. FocusAnchor remains fully usable offline without
an account. When enabled, a local calendar can be linked to one writable Google
calendar and synchronized manually.

Setup instructions are in
[docs/google-calendar-setup.md](docs/google-calendar-setup.md).

## Product Notes

The product boundaries and core loop are documented in
[docs/product-brief.md](docs/product-brief.md).
