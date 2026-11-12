# Spec Delta

## ADDED Requirements

### Requirement: A desktop edit the window did not draw says how far it got

A Photon app's hot reload SHALL say, as an edit arrives, how many types the edit changed and how many
windows it woke to draw it. When the desktop measurement does not see a window draw an edit, its
failure SHALL say what dotnet watch said after the save, what the app said of the edit, and whether
dotnet watch is still running.

#### Scenario: An edit that reached the app and was not drawn

- **WHEN** the measuring tool saves an edit of the desktop sample, the app reports the edit applied and
  the windows it woke, and no frame draws it before the limit
- **THEN** the failure quotes dotnet watch's lines and the app's after the save, so the reader sees the
  edit arrived and the window did not draw it

#### Scenario: An edit that never reached the app

- **WHEN** the save is followed by no line from the app's hot reload
- **THEN** the failure says the app said nothing, so no edit reached it, and whether dotnet watch is
  running or gone with its exit code
