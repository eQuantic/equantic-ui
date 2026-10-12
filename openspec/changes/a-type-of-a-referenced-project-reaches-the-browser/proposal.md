# Proposal

Closes #624, a Bug under #565 (The transpiler's fences hold on every path, continued). Falei.pt
reported it on `0.2.0-preview.60`, and Cura is waiting on it to split its backoffice into a second
app that shares a class library with the first.

## Why

A component that uses a type from another project of the solution builds with no diagnostic and
fails in the browser. A static member of a class library (`NickColor.Palette`) is
`ReferenceError: NickColor is not defined`, and so is a record of it that the page builds
(`new RoomCursor(...)`).

The cause is one fact, measured in Roslyn. eqc decides that a type is the app's own, and so has a
module to write and to import, by asking whether it is declared in source, in 51 places. A
referenced project reaches eqc only as the assembly MSBuild resolved (`--refs`), and a type read
from an assembly has no source: `Locations.Any(l => l.IsInSource)` is false and it has no declaring
syntax. So eqc translates the call by the type's name and writes no module for it. The same type
read through a reference to the project's own compilation answers true, with its declaration in the
library's file.

`Sdk.targets` hands eqc the app's directory and the SDK's own libraries, and nothing says where a
referenced project's source is. An app that outgrows one project has no way through today.

## What Changes

- The SDK hands eqc every project the app references, directly or through another project, with
  the directory its source is in. Nothing is set by hand. The SDK's own projects and icon packs are
  not among them.
- eqc builds one compilation per referenced project, from that project's source, and the app's
  compilation references them as compilations, where it referenced their assemblies. A type of a
  referenced project is then a type declared in source for every rule eqc has, with its own
  project's `global using` directives and internal types kept to itself.
- A type of a referenced project becomes a module of the bundle when a module the build writes
  imports it, and only then: a record the page builds, a static class whose member it reads, a
  component it places in its tree, and whatever those reach in turn. A type the browser code never
  reaches is never compiled, so a Domain or a Persistence project costs nothing and raises nothing.
- A reached type is transpiled as the app's own are. What it cannot carry to the browser is
  reported at its own file, a `[ServerOnly]` one is refused where it is named (EQ2010, as today),
  and one whose name an app type already holds is the collision the build already reports (EQ1005).
- A string a reached type reads from its own project's `.resx` reaches the client catalogs.

For a developer: a class library referenced with a `ProjectReference` is shared between apps, and
what a page uses of it works in the browser as it does on the server. No setting, no attribute.

Not here, each to be filed under #565 or the feature it belongs to: the declarative factory surface
for a referenced project's components (a page writes `new Wordmark()` until then), a `[Page]` or a
`[ServerAction]` declared in a referenced project, what a source generator writes in a referenced
project, hot reload on a referenced project's files, and a type of an assembly the build has no
source for (a package), which still reads as a name nothing defines and wants a diagnostic of its
own.

## Capabilities

### New Capabilities
- `referenced-projects`: which types of the projects an app references reach the browser, how the
  build finds them, and what it reports for one that cannot cross.

### Modified Capabilities
<!-- None. The rules a reached type is transpiled by are the ones the transpiler specs already hold
     for an app's own types. -->

## Impact

- `eQuantic.UI.Sdk` (`Sdk.targets`): writes the referenced projects for eqc. No new property: the
  developer surface (`developer-surface.baseline.txt`) does not move.
- `eQuantic.Build` (eqc): a new `--projects` option, and its build loop moves into the compiler
  library so a test can run it.
- `eQuantic.UI.Compiler`: per-project compilations, a semantic model found in the compilation that
  holds a tree, the dependency scan over referenced projects, and modules written on demand. Its
  `PublicAPI.Unshipped.txt` gains what is public of that. No app references this assembly.
- A sample under `samples/` with a shared class library, built by CI's `samples` job.
- The wiki (BuildFlow and SupportedFeatures, in English and Portuguese) and `docs/LEDGER.md`.
- No runtime, realizer, shell or template change. Nothing an app wrote stops compiling.
