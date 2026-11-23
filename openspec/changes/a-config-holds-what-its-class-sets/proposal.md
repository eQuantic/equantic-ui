## Why

Closes #1823, a Bug under #1587.

A transpiled object initializer on a type whose twin the runtime writes by hand reaches that twin as its
trailing config: `new BoxStyle { Foreground = token }` is `new BoxStyle({ foreground: token })`. The config is
typed by the interface, or the literal, the twin's constructor takes, and nothing compared it with the C# class
it mirrors. So a member C# lets an initializer set and the config lacks is a TS2353 that the runtime's `tsc`
raises only once some component first sets it. `BoxStyleConfig` lacked `foreground`, which `BoxStyle` has carried
since #614, until a slice of #1606 set it and had to stop and repair the mirror.

Measured on main, 183 members differ:

- `foreground`;
- the node members every `VisualNode` carries (`bookmark`, `alignSelf`, `gridSpan`, and `key` or `globalKey`
  where a config did not repeat them), missing from eight named node configs and nineteen configs written in
  place;
- `Spacer`'s `FixedLength`;
- 53 members a constructor also takes as a parameter. eqc writes what an initializer sets into the config
  wherever else the constructor could have taken it: `new Image(src, 1, 2) { Label = "x" }` is
  `new Image(src, 1, 2, 'cover', '', { label: 'x' })`. These are every single-child node's `child`, a text's
  `role` and `maxLines`, an image's `label` and `fit`, a gradient's `direction`, and the like.

## What Changes

For a developer using the SDK: an initializer that sets any member C# lets it set on a vocabulary type compiles,
and the browser's twin holds the value. Each of these failed the runtime's type check on main, and passes now:

- `new Group(child) { Bookmark = "totals" }`
- `new Spacer { FixedLength = 8 }`
- `new BoxStyle { Foreground = token }`
- `new Text("Totals") { Role = TypeRole.Title }`
- `new Image(src, 1, 2) { Label = "Chart" }`

- **The mirror's configs hold what C# sets.**
  - `BoxStyleConfig` gains `foreground`.
  - A `VisualNodeConfig` holds the members every node lets an initializer set, and a `SingleChildNodeConfig`
    adds the `child` of every node with one. The node configs extend them when named, and intersect them
    (`VisualNodeConfig & { … }`) when written in place.
  - Every member a constructor takes and C# lets an initializer set joins its config, `Spacer`'s `fixedLength`
    included.
  - Every one of those constructors already applied its whole config after its parameters, so the value an
    initializer sets wins, as in C#. The JavaScript is unchanged: every fix is type-only.
- **A test pins every config to its C# class, both ways.**
  - `VocabularyConfigShapeTests` reads each config a hand-written twin's constructor takes, named or written in
    place, and compares its members with the C# class's public settable members.
  - A member absent on purpose is named in `vocabulary-config.baseline.txt` with its reason, and that list may
    only shrink. Two entries start it: `VisualNode.origin` and `originLabel`, which only a design-mode
    compilation sets on the C# tree.
  - `EqConfig`, a record of anything, takes every member and is not read.
- **eqc's own output is checked.** `EmittedConfigs` joins the emission shapes transpiled into the runtime's
  fixtures. The runtime's `tsc` reads what eqc writes for eight such initializers, and a spec runs them and reads
  each value back.

Parts reached: the runtime's hand-written mirror (`src/shared/vocabulary.ts` and `value-types.ts`, types only)
and the tests. No public surface and no developer surface moves.

Rules of `docs/PRINCIPLES.md` followed:

- an instrument that fails beats one that warns: a test that compares and fails, with a list that may only
  shrink, rather than a slice meeting the drift;
- a coverage suite enumerates by reflection against a baseline.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-classes`: one new requirement, a hand-written twin's config takes every member its C# class lets an
  initializer set.

## Impact

`tests/eQuantic.UI.Web.Tests` gains `VocabularyConfigShapeTests` and its baseline. It also gains emission shapes in
`SharedComponentTranspilationTests`, written to `src/eQuantic.UI.Runtime/src/shared/__fixtures__/EmittedConfigs.ts`
and run by `vocabulary-config.spec.ts`. On main's mirror the shape test fails with 183 offenders, and the runtime's
`tsc` refuses the fixture eight times with TS2353.
