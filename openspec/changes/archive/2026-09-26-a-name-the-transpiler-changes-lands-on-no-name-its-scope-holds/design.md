# Design

## How Flutter answers it

It has no bearing: Dart compiles its own identifiers, and the question here is how a C# name maps
onto JavaScript's, which Flutter never has to answer. docs/FLUTTER-PARITY.md has no row for it.

## Decisions

- **The rename is a `$`, not an underscore.** No C# identifier can hold a `$`, so a renamed name can
  never meet a C# name, where the old trailing underscore could (`package_`).
- **The casing stays.** A local function keeping its C# spelling would never meet a local, but it
  would shadow the PascalCase names a module imports (`Row`, `Text`) wherever it is in scope.
- **The lowerings' own bindings are held by construction.** They are spelled with an underscore
  (`_idx`, `_sum`), and casing never produces one, so a function written with an underscore always
  takes a `$`, rather than a list of every binding a lowering declares.
