# Proposal

Closes #1278, a Task under #900.

## Why

The fourth of the seven slices that bring the web's edit-to-view under a second (#900). The eqc the
server keeps running (#1276) loaded the project's compilation from nothing on every compile: every
reference read and bound again, every file parsed again, 3.4s of a save's compile before anything
was transpiled. A save changes the text of a file, and nothing else a load reads.

## What Changes

- A build of a served eqc starts from the compilation the build before it loaded (`AppBuild.LoadFrom`).
  Its references stay read and bound, and each file whose text changed has its tree replaced in place
  (`Compilation.ReplaceSyntaxTree`), so the trees keep the order a cold load gives them.
- Anything a cold load would read differently loads cold: a file added or removed, a changed input the
  SDK handed (the references, the projects, the reference sources, the restore graph), or a changed
  file of a project the app references.
- The served eqc loads the project once before the first request, so the first save starts warm too.
- Nothing a developer writes changes. The compiler's surface, a tool's, gains `AppBuild.LoadFrom`.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `hot-reload`: a served eqc keeps the project's compilation between saves.
