# Design

## Context

See proposal.md for what fails and why it matters. The constraints that shape the fix:

- The runtime's twin of `Color` is a plain `{ r, g, b, a }` and a companion object of statics
  (`shared/value-types.ts`). The companion already carries `withOpacity(color, opacity)` and
  `midpointWith(color, other)`, byte-exact with the C#. Every other vocabulary value type
  (`ColorToken`, `EdgeInsets`, `Rect`, `TypeStyle`…) is a runtime class.
- A `Color` reaches browser code from four producers: code eqc emits, the runtime itself (ten
  literal sites in `style-atomizer.ts`, `theme-bridge.ts`, `vocabulary.ts` and `value-types.ts`),
  the generated design system, and a hydration payload, where it is JSON and the spec rebuilds
  nothing (`HydrationSpec`'s member map is empty for a type whose members need no coercion).
- Equality, `with`, deconstruction and the channels already answer as in .NET through
  `$eq.equals` (structural on a plain object), `$eq.withPatch` and property reads.
- eqc's records write their own `toString()` in .NET's record text (`RecordTypeEmitter`), and
  `ValueFlow` already owns every value on its way into text.

## Goals / Non-Goals

**Goals:**

- One rule, keyed by symbol, that every lowering of a member of such a type reads.
- An instrument that fails when a member of such a type has no browser answer, rather than a list
  someone remembers to extend.

**Non-Goals:**

- `GetHashCode`. No type has a lowering for it, records included, so it is filed on its own.
- Turning another vocabulary value type into plain data, or `Color` into a class.
- Changing the runtime's producers of a colour.

## Decisions

### The twin stays plain data, and its members go to the companion

Flutter's `Color` is a class with `withOpacity`, `withAlpha` and `lerp` on every target, because
nothing stands between the code that makes a colour and the code that calls it. Here four producers
make one and one of them is a JSON payload. A class would need every producer to construct it and
the hydration spec to rebuild it, or `withOpacity` would throw on exactly the colours that crossed
the wire. A companion function answers whichever producer made the value. So the browser keeps
`Color` as data, and eqc lowers its members to the companion. No row of `docs/FLUTTER-PARITY.md`
covers a value type's twin. This difference comes from the server-to-browser wire, which Flutter
does not have.

Alternative considered: make the runtime's `Color` a class like `ColorToken`. Rejected for the
producers above. The fix would grow into the runtime, the generated design system and the
hydration spec, and every one of them would be a new place the bug could come back.

### The rule is an attribute on the vocabulary type, read by symbol

`eQuantic.UI.Primitives/Contracts` gains an attribute beside `ConversionPassesThrough`, carrying
the reason its twin is data (working name `[PlainDataTwin(reason)]`, settled in review), and
`Color` wears it. eqc asks the symbol, so the name test in the object-creation strategy goes, and an
app's own `Color` is an app type.

Alternatives considered: a symbol test hard-coded in eqc, which is one more list to drift; and
deriving the shape from the runtime's exports, which eqc does not read. The coverage suite pins the
other direction, so the attribute cannot drift from the runtime either: a marked type whose runtime
export is a class fails it, and so does an unmarked vocabulary record or struct whose export is a
plain object.

### Three lowering points read the one rule

- **An instance method** on a marked type becomes the companion's static with the value first,
  `Color.withOpacity(value, 0.8)`. This is the shape the extension-member lowering already emits
  for an extension home. A method group of one (`Func<float, Color> f = c.WithOpacity`) becomes an
  arrow over the same call.
- **Text**, from `ToString()`, an interpolation hole or a concatenation, goes through `ValueFlow`'s
  text rule to a runtime helper that writes .NET's record text, `Color { R = 1, G = 2, B = 3, A = 4 }`.
  eqc passes the member names in declaration order, which is the order .NET's `PrintMembers` uses,
  and each value goes through the same text path as an interpolation hole.
- **A construction** of a marked type builds its data: `new Color(1, 2, 3, 4)` becomes
  `{ r: 1, g: 2, b: 3, a: 4 }`, from the positional members by symbol. `new Color()` and an object
  initializer start from the members' defaults. No per-type factory name has to stay in step.

### The coverage suite executes every member on both sides

A conformance test enumerates by reflection the public instance methods of every type the
attribute marks, plus its text. It builds each call from the parameter types with fixed sample
values, transpiles it, runs it in the embedded Bun against the runtime, and compares the result
with .NET's. A parameter type the suite cannot build a sample for fails the suite by name, so a new
member cannot slip past it. The same suite loads the runtime bundle and checks each vocabulary value
type's export against the attribute (an object for a marked type, a class for every other one).

## Risks / Trade-offs

- [A new instance method on `Color` without its companion static] → the coverage suite runs it and
  fails with the method's name.
- [A member reached a way the three points do not see, for example through an interface `Color`
  implements] → `Color` implements none beyond the record's own, and the suite enumerates the
  members a component can call. A call through a generic constraint stays emitted as a method, as
  it is today.
- [The record text depends on how each member prints] → `Color`'s members are bytes, and the helper
  formats every member through the interpolation path, so a future member prints as .NET's does.

## Migration Plan

None. The fix changes no C# a developer writes, and nothing that worked before changes its answer.
