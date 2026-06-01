# Google Calendar Setup

Google Calendar support is optional. FocusAnchor works entirely offline when it
is not configured, and local changes remain available if a sync attempt cannot
reach Google.

## Create the OAuth Client

1. Open the [Google Cloud Console](https://console.cloud.google.com/).
2. Create or choose a project and enable the Google Calendar API.
3. Configure the OAuth consent screen for the intended users.
4. Create an OAuth client ID with application type **Desktop app**.
5. Copy the generated client ID. Do not add a client secret to the repository.

FocusAnchor uses the system browser, PKCE and a loopback redirect on
`127.0.0.1`. It requests only these scopes:

```text
https://www.googleapis.com/auth/calendar.calendarlist.readonly
https://www.googleapis.com/auth/calendar.events
```

## Configure FocusAnchor

Set the client ID before launching the app:

```powershell
$env:FOCUSANCHOR_GOOGLE_CLIENT_ID = "your-client-id.apps.googleusercontent.com"
dotnet run --project src/FocusAnchor.App
```

Open **Calendario**, select a local calendar and choose **Vincular Google**.
After authorizing in the browser, select a writable Google calendar and save the
link.

## Sync Behavior

- Sync runs only when **Sincronizar ahora** is selected.
- FocusAnchor writes only events that carry its private `focusAnchorPlanId`
  property.
- Other Google events appear as read-only context and are never edited.
- When both copies changed after the previous sync, FocusAnchor shows a
  conflict and asks which version to keep.
- Local edits remain pending after a network failure and can be retried later.

## Credentials and Local Storage

The refresh token is stored in Windows Credential Manager under:

```text
FocusAnchor.GoogleCalendar.RefreshToken
```

Access tokens stay in memory only. The OAuth client ID is read from the
environment and no Google credentials belong in source control.

The local SQLite database is stored at:

```text
%LOCALAPPDATA%\FocusAnchor\focus-anchor.db
```

Set `FOCUSANCHOR_DATA_PATH` to override the containing directory during local
development or smoke tests.
