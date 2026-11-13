# Proposal

Closes #1280, a Task under #1277 and #900.

## Why

The sixth of the seven slices that bring the web's edit-to-view under a second (#900). Every save started
a new bun and handed it every entry point, and bun wrote every module, chunk and map again, so the
bundle's part of a save grew with the app and every file's time moved with nothing in it changed.
Measured on the dashboard sample at one load, bun's own work was the smallest part of it: bun bundled the
22 entry points in 30 to 44 ms inside a process it kept, and took 117 to 215 ms started anew from eqc,
and composing all 24 maps again took 74 to 159 ms more.

## What Changes

- **A bundler keeps one bun between bundles.** `ModuleBundler` is a class with an instance now: its first
  bundle starts a bun process (`bun -e` over the worker script eqc carries), and every later bundle is
  handed to the same process. The served eqc (#1276) keeps one for the life of the server, so a save
  pays for starting bun once. A build that is not served starts bun for its bundle and stops it after.
- **Every entry point is still bundled every time,** with `--splitting`, so the chunks the pages share
  stay the ones every page imports: a page bundled alone would carry its own copy of a module it shares,
  and two copies of a module are two of its state.
- **What did not change is not written.** The worker bundles in memory and writes a module, a chunk or a
  map only when its text changed, a map also when a map it composes with changed (the TypeScript of a C#
  file whose lines moved can be the same text with another map). Only the maps it wrote are composed. The
  prune goes by what the bundle produced, as #1279 made eqc's.
- **What a publish copies is the bytes it was:** a bundle with no maps is byte for byte what bun's
  command line wrote with the flags eqc handed it, chunk names included. A Debug bundle's module differs
  from the command line's only in bun's `debugId`.

Nothing an app writes changes. The compiler's surface, a tool's, changes `ModuleBundler` from a static
`Bundle` to an instance with `Dispose`, and `AppBuild` takes the bundler a caller keeps.

## Capabilities

### Modified Capabilities

- `hot-reload`: a served eqc keeps one bun between saves, and a save's bundle writes what changed.

## Impact

- `eQuantic.UI.Compiler`: `ModuleBundler` (an instance and its worker, `Services/BundleWorker.js`, an
  embedded resource), `AppBuild`'s constructor, declared in `PublicAPI.Unshipped.txt`.
- eqc: the served loop keeps the bundler.
- The runtime, the realizers, the Photon shells, the SDKs' targets and the templates: no change. The
  developer surface does not move.
- `scripts/edit-to-view.budget.json`: the web's ceiling comes down by what was measured.
- Docs: a ledger entry.
