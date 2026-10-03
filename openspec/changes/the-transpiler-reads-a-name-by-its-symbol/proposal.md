# Proposal

Closes #479, #485 and #517, sub-issues of #164 ("The transpiler's fences hold on every path", under
the epic #157).

## Why

eqc decided two names by the way the source SPELLED them, where the semantic model already knew the
symbol, and both failed only in the browser.

- A base written with its namespace, `class Tag : eQuantic.UI.Web.HtmlElement`, was copied into the
  module as `extends eQuantic.UI.Web.HtmlElement`, a name nothing defines, and the module failed when
  it loaded (#479). An alias or `global::` failed the same way.
- A .NET member reached bare through `using static` fell to the rule for the app's own statics:
  `NaN` read `Double.naN`, `PI` read `Math.pI`, `Empty` read `String.empty`, and `Join(",", parts)`
  called `String.join`, each a member of a class nothing defines. `Round(x)` called JavaScript's
  `Math.round`, which sends a half up where .NET sends it to even (#485). An enum's member reached
  bare read `Level.high` where the member is `'high'`.
- A type's own `Count` was read as an array's `.length` unless the type was in the app's source, so
  the domain model of a library the app references counted `undefined` (#517).

## What Changes

- A base type is named after its twin, from the symbol: the class's own name, whatever the spelling
  (namespace, alias, `global::`), and the module imports it as it imports every other name.
- A .NET member reached bare through `using static` translates as its qualified spelling does: a
  constant inlines, `Math` and `string` members and the primitives' statics go through the same
  tables, and an enum's member is the member. One that no translation covers fails the build with
  EQ2004, never a camel-cased guess.
- A type that is not one of .NET's collections reads its own `Count`, wherever it is declared.
- Nothing a developer writes changes. Code that compiled and failed in the browser now either runs
  as on the server or fails the build, naming the member.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-names`: a base type is emitted under its twin's name.
- `transpiler-bcl`: a member reached through `using static` answers as its qualified spelling.

## Impact

- **eqc**: the component parser, the plain-class and record emitters (base names), the constant,
  `Math`, `string` and enum strategies, and the identifier and invocation fallbacks.
- **Public surface**: none. The developer surface does not move.
- **Break**: a `using static` of a .NET type whose member no strategy translates was emitted and
  threw in the browser. It now fails the build with EQ2004: write it qualified where that form
  translates, or keep it on the server.
