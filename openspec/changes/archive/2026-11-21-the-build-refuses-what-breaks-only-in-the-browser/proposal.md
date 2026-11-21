# Proposal

Closes #1776 and #1777, two sub-issues of the epic #1774 ("Lessons from a real site: what fails only in
the browser is caught by the build and the tests").

## Why

An app built on the SDK from an empty repository to production kept a list of pitfalls in its own
`CLAUDE.md`, each found by opening a page after the build and the server's tests were green, and
guarded one of them with a test that scanned its own sources. Measured against `main` before writing
anything, the list splits in three:

- **Already refused or already right.** `string.Format` translates (EQ2100 and EQ2108 hold what it
  cannot), an overload is EQ1007, and a `long` divided by a literal is a BigInt quotient that C#'s
  implicit conversion turns into the number a vocabulary parameter takes, on the build an app runs.
  Nothing to add but a test that holds the last claim.
- **Silent, and a diagnostic is the answer.** An `Icon` of 22 dp builds, renders and throws when the
  node is built; a resx accessor read into a static keeps the first reader's language; a type of the
  app's named like a type of the SDK's, used beside it in one module, is imported once, so one of the
  two silently becomes the other.
- **Silent, and the SDK was wrong.** A stateful section a page builds with the values its prefetch
  loads kept the instance the server built BEFORE the prefetch ran: `Page: 675617` over
  `Section: -1` in the served HTML, every time.

## What Changes

- **EQ3106 (error).** A constant handed to a parameter outside the values it allows, read from .NET's
  own `[AllowedValues]`. `Icon`'s constructor and its factory mark `size` with the four steps of
  `IconSize`, so `Icon(glyph, 22)` fails the build, in the editor as it is typed. An app's own
  parameter marked `[AllowedValues]` is read the same way.
- **EQ3107 (warning).** A resx accessor read once, into a static: a static field's or property's
  initializer, or a static constructor. A getter, a method, a lambda and an instance's field are left
  alone.
- Both are Roslyn analyzers linked into the web SDK's generator AND the native SDK's, since a Photon
  app meets both pitfalls the same way. The catalog's shape is one file (`ResourceShape`) eqc and the
  analyzer both read.
- **EQ1005 widens.** A module that names a type of the app's and a type of the SDK's under one name
  (the app's `Text` record beside the vocabulary's `Text`, a record `Card` in a screen that shows the
  SDK's `Card`) is refused, naming both, at the module's declaration. Two types of one name in one
  file, in different namespaces, are EQ1005 too, naming both, where they were EQ2009's "partial
  declarations?". Two types of the app's in different files were already EQ1005.
- **The server draws a stateful section with what its page loaded.** While the server's data rounds
  run, a position keeps its stateful instance only when that instance loads data
  (`IServerPrefetch`); any other takes the fresh instance its parent hands it. `BuildOwner` gains
  `KeepsInstance`, which the rounds arm and a tree drawn for a person leaves null, so nothing changes
  once a page is drawn, in the browser or in the tester.

What a developer writes does not change. A constant icon size off the scale, a static read from a
catalog and a type named like the SDK's stop at the build instead of in the browser; a stateful
section handed loaded values shows them.

## Capabilities

### New Capabilities

- `build-analyzers`: what the SDK's Roslyn analyzers report in an app's code, in every build and in
  the editor.

### Modified Capabilities

- `transpiler-names`: a module never imports one name for two types.
- `element-tree`: while the server loads a page's data, only what loads keeps its instance.

## Impact

- eqc (`ComponentCompiler`, `NamedSourceTypes`, `CompilationExtensions`, `ResourceClasses`), the two
  source generators (`src/Shared/AllowedValueAnalyzer.cs`, `CultureFrozenResourceAnalyzer.cs`,
  `ResourceShape.cs`), the element engine (`BuildOwner`, `ComponentElement`, and their committed
  transpiled twins), the web realizer's `ServerDataRounds`, `Icon` and `UI.Icon`.
- Public surface: `BuildOwner.KeepsInstance` (get and set) is added to `eQuantic.UI.Primitives`.
  Nothing is removed. The developer surface does not move.
- No break: code that built and ran keeps building and running, except a constant icon size off the
  scale, which already threw, and a module that already imported the wrong type.
- Rules followed: 1 (the developer states nothing new), 9 (the structural fix for the stateful
  section, the mechanism .NET has for allowed values, and an error over a warning where the code is
  certainly wrong), 10 (the values are read from the mark, the catalog from its shape), 14 (the
  stateful section was the SDK's to fix, not the app's to work around).
