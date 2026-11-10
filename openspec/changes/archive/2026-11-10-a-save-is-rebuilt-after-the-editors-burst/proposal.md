# Proposal

Closes #1274, a Task under #900, and #1262, a Bug under #1174.

## Why

The first of the seven slices that bring the web's edit-to-view under a second (#900). Two things
happen to a save before any compile starts. The server waits a fixed 400ms after the last change it
sees, which #1261 measured as 44% of the whole bar. And a save that arrives while a rebuild runs is
dropped (#1262): `Rebuild` returns when the flag is already taken and nothing runs another rebuild
when the first one ends, so the browser stays on the edit before it until the next save. A rebuild
takes seconds, so two saves inside one are the ordinary case.

## What Changes

- The wait is a 50ms quiet window that each change of a save restarts, so an editor's burst of writes
  is still one rebuild, and a save costs tens of milliseconds instead of 400.
- A save during a rebuild is marked pending before the flag is tried, and the rebuild holding the flag
  runs once more when it ends: once, however many saves came, since a rebuild compiles the whole
  project. A save between its last look and the release is run by the same loop.
- The window is one timer for the service's life, restarted with `Change`, where each event replaced
  it from a pool thread and two at once left the first one running. An event still in flight when the
  app stops changes nothing.
- Nothing a developer writes changes, and neither the public surface nor the developer surface moves.
  The part this reaches is the Server's hot reload service.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `hot-reload`: a save is rebuilt after a short quiet window, and none is dropped.
