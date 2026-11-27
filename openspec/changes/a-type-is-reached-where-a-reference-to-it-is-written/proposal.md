# Proposal

Closes #830, a Bug under #565.

## Why

A page shows the app's own `Order`, a record, and names a referenced project's `Order`, a class that reads a
file, in a cast of a null: `(Domain.Order?)null`. The build failed at the Domain's file (EQ2112), though the
module writes nothing of the Domain's `Order`. Take the app's `Order` away and the same page builds. So what a
page may write depended on a name it does not use.

The module's `./Order` was mapped back by its name to every declaration a walk of the page's syntax named
under it. #827 took out of the walk what an owner could answer for (`nameof`, an attribute, a constant written
as its value). What was left is syntax that names a type and writes nothing of it: a cast of a null, a
`default`, a delegate's type, a constant in the old `is` form, a member of an enum nested in the class.

## What Changes

- **A writer records what it wrote.** Each writer of a twin's name (a construction, an annotation, a class's
  base, a static call or read, a reduced extension's home, a hydration spec, a union's cases) records the
  symbol it wrote the name for (`WrittenTwins`).
- **An import means what was written.** Where every use of a name in the module is a write on record, the
  module's `./Order` means the types those writes were of and nothing else (`ReferencedNames.ImportedBy`).
- **An unrecorded writer costs precision, never a type.** Where a writer wrote the name and recorded nothing,
  some use is not on record, and the import means every type of the name the module's symbols name, as before.
- **The rest is held.** `CompilationResult.ImportsOnTheWalk` names the imports still decided the old way, and
  `ImportsOnTheWalkTests` holds them over the shared library, the emission shapes and the fixtures, in a baseline
  that may only shrink.

Nothing an app writes moves, and no module's text moves: the change reads what the writers write, and writes
nothing new. The compiler gains `CompilationResult.ImportsOnTheWalk`, declared in the branch's own
`PublicAPI.Unshipped` file. No developer surface moves.

## Impact

- `src/eQuantic.UI.Compiler`: `WrittenTwins`, the writers that record into it, `ReferencedNames.ImportedBy`.
- `tests/eQuantic.UI.Compiler.Tests/ReferencedProjects`: the shapes, both ways.
- `tests/eQuantic.UI.Web.Tests`: `ImportsOnTheWalkTests` and its baseline.
- `openspec/specs/referenced-projects`: "Only what the browser code reaches is compiled".
