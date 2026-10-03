# Proposal

Closes #494, a sub-issue of #164 ("The transpiler's fences hold on every path", under the epic #157).

## Why

`Color` is the one vocabulary value type whose browser twin is its data alone: a plain
`{ r, g, b, a }`, with the type's statics on a companion object (`Color.fromRgb`,
`Color.withOpacity(color, opacity)`). eqc emits an instance member of a `Color` as a method of the
value, which a plain object does not have, so a component that compiles and renders on the server
fails in the browser. equantic.tech met it on its auth page:
`Color.fromRgb(...).withOpacity is not a function`.

Measured on d09f7bec, transpiling a component that uses a `Color` every way it can:

- `WithOpacity` and `MidpointWith` are emitted as `value.withOpacity(…)`, and throw.
- `ToString()` is emitted as `String(value)`, which prints `[object Object]` where .NET prints
  `Color { R = 248, G = 113, B = 113, A = 204 }`, and so does an interpolation hole.
- `==`, `!=`, `Equals`, `with`, deconstruction and the channels already answer as in .NET, through
  `$eq.equals`, `$eq.withPatch` and plain property reads.
- `new Color(…)` is recognized by the type's NAME (`typeName == "Color"`), so an app's own type
  named `Color` would be built as the vocabulary's.

## What Changes

- A developer's `Color.FromRgb(0xF8, 0x71, 0x71).WithOpacity(0.8f)` and `a.MidpointWith(b)` run
  in the browser as they do on the server, and `$"{color}"` prints what .NET prints. Nothing the
  developer writes changes.
- The vocabulary says which of its value types the browser holds as plain data, by symbol, with an
  attribute on the type in `eQuantic.UI.Primitives` (`Color` is the one today). eqc reads it for
  every member of such a type: an instance method becomes the companion's static with the value
  first, the text of the value is the record's text, and a construction builds the data.
- The name test in the object-creation strategy goes. An app's own `Color` is an app type again.
- A coverage test enumerates, by reflection, every public instance member of every type the
  attribute marks, and executes each one on both sides: the server's answer and the browser's must
  agree.
- Out of scope, filed on its own: `GetHashCode` has no lowering for any type, so every call throws
  in the browser, a record's as well as a `Color`'s.

## Capabilities

### New Capabilities

- `transpiler-vocabulary-values`: what a member of a vocabulary value type does in the browser,
  where the runtime, not eqc, ships the type's twin.

### Modified Capabilities

(none)

## Impact

- **eqc**: the object-creation strategy, the lowering of an invocation on a value, and the
  conversion to text (`ValueFlow`'s text rule), all reading one predicate.
- **The runtime**: a text helper for a value whose twin is plain data. The companion's statics
  (`withOpacity`, `midpointWith`) exist already.
- **`eQuantic.UI.Primitives`**: one new public attribute, so the public surface moves (an addition
  to `PublicAPI.Unshipped.txt`). The developer surface (csproj, appsettings, templates) does not.
- **Not reached**: the web realizer, the Photon shells, the SDKs and the templates. The server
  already runs the C# itself.
- **Break**: none.
