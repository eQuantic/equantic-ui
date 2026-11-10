# Proposal

Closes #1373, a Bug under #1174 (a sub-issue of #157).

## Why

A web save sometimes reloaded the page into a document that drew over the server's markup instead of hydrating it, and the `samples` job's edit-to-view measurement failed the run about once in four. That job is required, and the queue merges ALLGREEN, so the failure took pull requests out of the queue.

The events of one save can straddle the start of its rebuild: the file watcher raises more than one for a write, and the debounce asks for a rebuild again when one comes after the first rebuild took the save. That second rebuild had no save to read a write time from, so it took the time it was asked, waited for an edit after it, found none, and told the page to mount. The page the first rebuild had reloaded into hydrating markup reloaded again, and drew over it. The measurement could not say so: it recorded neither the frame the page heard nor why the server answered.

## What Changes

- A rebuild answers the saves made since the last one took them, and a call that finds none rebuilds nothing and tells the page nothing.
- The server writes why each rebuild tells the page to hydrate or to mount: the save's write time and when the last edit reached it. `scripts/measure-edit-to-view.cs` records the frames each document's stream heard, what the reload carried and whether the server rendered the document, and a save that draws over fails naming all of them with the server's line.
- Nothing a developer using the SDK writes changes. Under `dotnet watch`, a save reloads the page once, and the server's console says why the page hydrated or drew over.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `hot-reload`: a save is announced once.
