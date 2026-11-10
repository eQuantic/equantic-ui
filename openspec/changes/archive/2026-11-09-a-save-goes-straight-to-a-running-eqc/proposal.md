# Proposal

Closes #1276, a Task under #900.

## Why

The third of the seven slices that bring the web's edit-to-view under a second (#900). Every save
started a new `dotnet msbuild -t:CompileEQuanticUI`, which evaluated the project and resolved its
references again (beside a source tree, it built the referenced projects too, the Server's runtime
bundle included) before it started a new eqc, which loaded the compiler and the project's
compilation from nothing. CI's first clean run measured 6.8s of MSBuild before a module was written,
and eqc's part included its process start. MSBuild decides nothing a save changes: the project file
and the references are not among what a save edits, and dotnet watch restarts the app when they
change.

## What Changes

- The server asks MSBuild ONCE for a run how to start eqc: the target that writes eqc's response file,
  and the properties that say where eqc and that file are. It does it while the app starts, beside the
  app's own start, so the first save pays none of it.
- eqc stays running: `eqc @<file> --serve` reads `compile` on its standard input and answers each in
  one line, `@eqc` and a JSON object saying whether it compiled and holding its error lines. Each
  compile loads the project's compilation again from the sources as they are, and the compiler itself
  is loaded once, before the first request.
- The server owns the process. eqc ends with its standard input, so a server that stops or is killed
  takes it along. A stop, or a compile past its limit, kills it, and the next save starts it again, as
  it does after eqc dies. A compile that fails answers with its errors, which reach the development
  overlay, and the next save compiles again with no restart.
- eqc is started without the variables dotnet watch gives the app, which would load dotnet watch's
  agent into it and contend for the app's channel to dotnet watch.
- `--watch`, which compiled again with the semantic model it loaded at its start, gives way to
  `--serve`. A response file may be followed by more arguments, which follow its own.
- Nothing a developer writes changes. The compiler's surface, a tool's, trades `AppBuildOptions.Watch`
  for `Serve`.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `hot-reload`: a save is compiled by an eqc the server keeps running, and no MSBuild runs for it.
