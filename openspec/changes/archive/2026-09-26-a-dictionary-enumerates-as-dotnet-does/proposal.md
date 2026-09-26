# Proposal

Closes #435, a Bug under #164 (The transpiler's fences hold on every path).

## Why

A `Dictionary<K, V>` with a primitive key is a plain JavaScript object on the web, and a JavaScript
object enumerates its integer-like keys in ascending order and hands every key back as a string.
.NET enumerates a dictionary by slot: in insertion order while nothing is removed, and a removed
entry's slot is the next one reused. Measured on main at b1a6a95d: `d[3] = 30; d[1] = 10;` gives
`Keys` "3,1" in .NET and "1,3" on the web, and 7 of 8 cases over int keys, integer-like string keys,
a removal and `ToDictionary` differ. Beside the order, three paths are broken outright: a dictionary
keyed by a `char`, a `Guid` or a date takes a plain path whose `foreach` throws, a
`foreach (var (k, v) in m)` over a record-keyed dictionary throws, and `new Dictionary<,>(other)`
aliases `other` instead of copying it.

## What Changes

- **Every dictionary is one runtime class**, the twin of .NET's `Dictionary<TKey, TValue>`: entries
  held by slot, a removed entry's slot reused first, last freed first reused, and each key kept in
  its own type. A key compares as .NET's default comparer compares it: by value for a record, a
  struct, a tuple, a decimal or a date, and as JavaScript's `Map` compares for a number, a string, a
  char, a bool, a long, an enum's name, a `Guid` and a class instance. `SortedDictionary` and
  `SortedList` keep their own classes.
- **eqc lowers every `Dictionary`, `IDictionary` and `IReadOnlyDictionary` through one strategy**,
  the map-backed one that record keys already used: construction, copy construction, initializers,
  the entry, every method and `Keys`, `Values` and `Count`. The plain-object lowering and its
  helpers go. `d.Keys.Contains(k)`, `d.Keys.Count`, `TryAdd` and `ContainsValue` answer on the
  same class, and a pair enumerated from a dictionary destructures as `(key, value)` and answers
  `.Key` and `.Value`.
- **`ToDictionary` builds the same class**, keyed as the target type would key it, so it no longer
  refuses a key a plain object could not hold (a `DateTime`, a class instance). A comparer and an enum
  with aliases stay refused.
- **The wire revives it**: every dictionary field and Server Action result carries a hydration spec
  now, keys included, so the JSON object becomes the class with its keys in their type. A dictionary
  sent to the server writes itself as the JSON object System.Text.Json reads.
- **The DOM escape hatch reads either form**: `HtmlElement`'s `BuildAttributes` and `BuildEvents`
  answer the class, as their C# signatures say, and the renderer, `DynamicElement`, analytics and
  route values read a bag whether a transpiled dictionary or a plain object hands it over.

For a developer using the SDK: a dictionary enumerates on the web as it does in .NET, its keys are
numbers where they are numbers, and a dictionary keyed by a char, a `Guid` or a date works. Nothing
is written differently. One limit stays, measured and given its own issue: a dictionary that crosses
the JSON wire arrives in the order the parsed JSON object holds, so an integer-keyed one arrives
ascending.

The parts reached are eqc, the runtime and its transpiled twins (six shared components), and the
served runtime's budget. The public surface of eqc moves (retired and added `Eq` constants and
strategy types, declared in `PublicAPI.Unshipped.txt`); the developer surface does not. The
migration line: a dictionary is the runtime's dictionary class on the web, not a plain object, so
hand-written JavaScript that indexed a transpiled dictionary with `d[key]` or `Object.keys(d)`
reads it with `get`, `keys()` and `for...of` instead.

## Capabilities

### New Capabilities

- `runtime-dictionaries`: what a C# dictionary answers on the web, from its enumeration order to its
  keys' types, and how it crosses the wire.

### Modified Capabilities

None.

## Impact

`src/eQuantic.UI.Compiler` (the dictionary strategies, the entry and lookup helpers, object
creation and initializers, `foreach`, member access, the BCL tail, `ToDictionary`, the hydration spec
and the TypeScript annotations), `src/eQuantic.UI.Runtime` (the dictionary class, hydration, LINQ,
the DOM escape hatch, the renderer, analytics and route values), the six regenerated twins, tests in
the compiler, conformance, web and runtime suites, the served runtime's budget, `docs/LEDGER.md`,
`docs/DOTNET-COVERAGE-PROGRAM.md` and the wiki's SupportedFeatures page in English and Portuguese.
