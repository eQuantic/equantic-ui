# Proposal

Closes #1275, a Task under #900.

## Why

The second of the seven slices that bring the web's edit-to-view under a second (#900). Each save
reloaded the page twice. dotnet watch applied the edit to the server and refreshed the browser
itself, so the page reloaded with the patched server's markup and the OLD modules. Seconds later the
rebuild reloaded it again with the new ones, into markup the boot could not trust, so it drew over it
instead of hydrating it. In between, the page ran code that did not match what it showed: the next
render of an old module could draw the old text back, and an edit to a handler did not show at all.

## What Changes

- The rebuild stream is the page's one source of reloads. While it runs, the shell sets the guard
  dotnet watch's refresh script keeps against connecting twice, so the script returns before it
  connects. A test reads the script out of the SDK global.json selects and fails when the guard is
  renamed, and the edit-to-view measurement fails when a save reloads the page more than once.
- The stream says three things. `server`, first on every connection, names the run of the server, so a
  page that reconnects to another run reloads, which dotnet watch's script did after a restart.
  `rebuilt` says the modules are new and whether the server's markup holds the edit (`hydrate`) or not
  (`mount`). `failed` carries the errors of a rebuild that did not compile, which the development
  overlay shows over the page, where dotnet watch's script showed the compiler's. A change to a file under
  the app's `wwwroot` reloads the page through the stream as well, as dotnet watch's script did.
- The server counts the edits applied to it through .NET's `MetadataUpdateHandler`. A rebuild
  announces itself once the edit of the save it answers has arrived, waiting up to five seconds from
  the save when dotnet watch runs the server, so the page reloads once, into markup that holds the
  edit.
- The boot hydrates that markup and replays the page's fields after it, as a change the next render
  draws: the server rendered the page with its own state. When the markup does not hold the edit, the
  boot draws over it with the fields restored first, as before.
- Nothing a developer writes changes, and neither the public surface nor the developer surface moves.
  The parts this reaches are the Server's hot reload service and shell, the boot and the development
  overlay.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `hot-reload`: a save reloads the page once, into markup that holds the edit, and the stream is the
  page's one source of reloads.
