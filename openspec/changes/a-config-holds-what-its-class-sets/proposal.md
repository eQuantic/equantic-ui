## Why

Closes #1823, a Bug under #1587.

A transpiled object initializer on a type whose twin the runtime writes by hand reaches that twin as its
trailing config: `new BoxStyle { Foreground = token }` is `new BoxStyle({ foreground: token })`. The config is
typed by the interface, or the literal, the twin's constructor takes, and nothing compared it with the C# class
it mirrors. So a member C# lets an initializer set and the config lacks is a TS2353 that the runtime's `tsc`
raises only once some component first sets it. `BoxStyleConfig` lacked `foreground`, which `BoxStyle` has carried
since #614, until a slice of #1606 set it and had to stop and repair the mirror. Measured on main, 126 members
differ: `foreground`, the node members every `VisualNode` carries (`bookmark`, `alignSelf`, `gridSpan`, and `key`
or `globalKey` where a config did not repeat them) missing from eight named node configs and nineteen configs
written in place, and `Spacer`'s `FixedLength`.

## What Changes

For a developer using the SDK: an initializer that sets any member C# lets it set on a vocabulary type compiles,
and the browser's twin holds the value. `new Group(child) { Bookmark = "totals" }`, `new Spacer { FixedLength = 8 }`
and `new BoxStyle { Foreground = token }` each failed the runtime's type check on main, and pass now.

- **The mirror's configs hold what C# sets.** `BoxStyleConfig` gains `foreground`. A `VisualNodeConfig` holds the
  members every node lets an initializer set; the eight named node configs extend it, and the nineteen configs a
  constructor writes in place intersect it (`VisualNodeConfig & { … }`). `Spacer`'s config gains `fixedLength`.
  Each of those constructors already copied its whole config onto the twin, so the values reach the node as C#
  sets them, and the JavaScript is unchanged: every fix is type-only.
- **A test pins every config to its C# class, both ways.** `VocabularyConfigShapeTests` reads each config a
  hand-written twin's constructor takes, named or written in place, and compares its members with the C# class's
  public settable members less its public constructors' parameters. A member absent on purpose is named in
  `vocabulary-config.baseline.txt` with its reason, and that list may only shrink. Two entries start it,
  `VisualNode.origin` and `originLabel`, which only a design-mode compilation sets on the C# tree.
  `EqConfig`, a record of anything, takes every member and is not read.
- **eqc's own output is checked.** `EmittedConfigs` joins the emission shapes transpiled into the runtime's
  fixtures, so the runtime's `tsc` reads what eqc writes for those three initializers, and a spec runs it.

Parts reached: the runtime's hand-written mirror (`src/shared/vocabulary.ts`, types only) and the tests. No public
surface and no developer surface moves.

Rules of `docs/PRINCIPLES.md` followed: an instrument that fails beats one that warns (a test that compares and
fails, with a list that may only shrink, rather than a slice meeting the drift), and a coverage suite that
enumerates by reflection against a baseline.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-classes`: one new requirement, a hand-written twin's config takes every member its C# class lets an
  initializer set.

## Impact

`tests/eQuantic.UI.Web.Tests` gains `VocabularyConfigShapeTests` and its baseline, and one emission shape in
`SharedComponentTranspilationTests`, written to `src/eQuantic.UI.Runtime/src/shared/__fixtures__/EmittedConfigs.ts`
and run by `vocabulary-config.spec.ts`. On main's mirror the shape test fails with 126 offenders, and the runtime's
`tsc` refuses the fixture three times with TS2353.
