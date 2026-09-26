# Design

## How Dart does it

No row of `docs/FLUTTER-PARITY.md` applies. Dart's default `Map` is a `LinkedHashMap`: it keeps
insertion order, and a key removed and added again goes to the end. .NET's `Dictionary` does not:
it reuses the removed entry's slot. The twin follows .NET, since the order a C# author sees is
.NET's.

## Context

Two representations live side by side today. A primitive key makes a plain object, lowered by
`DictionaryStrategy`, `DictionaryEntry.PlainObject`, `DictionaryLookup.PlainObject` and a gate per
construct (`IsDictionaryLike`, a display-string test, a written-type test). A record key or a
sorted dictionary makes a runtime map, lowered by `MapBackedDictionaryStrategy`. The gates disagree,
which is how a `char` or a `Guid` key fell between them, and how an `IReadOnlyDictionary` view of a
runtime map took the plain path.

## Decisions

### One runtime class, with .NET's slots

The twin keeps entries in an array of slots and a stack of freed ones. `set` takes the last slot
freed, or appends; `delete` frees its slot; enumeration walks the slots in order and skips the free
ones. That is .NET's `Dictionary` (its `_freeList` is last in, first out), so every order in the
spec follows from it. A JavaScript `Map` alone was the first candidate and measured wrong after a
removal: it would enumerate `b,c,d` where .NET enumerates `d,b,c`.

### A key compares as the default comparer does, decided by eqc

Where a key is found is the only place the key type matters. A number, a string, a char, a bool, a
long, an enum, a `Guid` and a class instance that does not override `Equals` are found through a
JavaScript `Map` of key to slot: its SameValueZero is .NET's default equality for each of them, NaN
included. A record, a struct, a tuple, an anonymous type, a decimal, a date and a class that
overrides `Equals` are found by `$eq.equals` over the live slots, which delegates to a twin's own
`equals`. eqc knows the key type and passes the choice to the factory, so the runtime never guesses
from a value. The rule is LINQ's for its keyed operators (`LinqKeys.ComparesByValue`) plus the
`Equals` override, which LINQ's tuple equality must not take since a tuple's `==` is its elements'
operators. `object`, an interface and a type parameter stay on the `Map`, as LINQ's keys do: a
record behind one of them compares by reference, the limit GroupBy already has.

`ContainsValue` compares a value by the same rule, applied to the value type, and `TryAdd` and
`ContainsValue` are methods of the class, so each argument is evaluated once and in its place.

### A pair is both shapes C# reads

Enumeration yields `[key, value]` arrays that also carry `.key` and `.value`: `foreach (var (k, v)
in d)` destructures it, and `kv.Key` reads it. The plain path's `$eq.entries` already built pairs
this way; the map path yielded `{ key, value }`, which is why its deconstructing `foreach` threw.

### One lowering

One strategy, `DictionaryStrategy`, born on the IR, owns every `Dictionary`, `IDictionary`,
`IReadOnlyDictionary`, `SortedDictionary` and `SortedList`, with the factory chosen by the type and
the key's comparison. The plain-object strategy, the abstract map-backed one and its two
subclasses, `IsDictionaryLike`, `$eq.entries`, `$eq.dictGet`, `$eq.collections.valueMap` and the
plain branches of the entry and lookup helpers go, with the tests that pinned their shapes, and the
entry and lookup helpers keep one form each. Construction takes a source dictionary (a copy, which
was an alias), an initializer of either form, or both, and refuses a comparer. `Keys.Contains`,
`Keys.Count`, `Values.Count` and `Remove(key, out value)` join the members lowered, and a
null-conditional write (`d?[k] = v`) and a property pattern's `Count` (`d is { Count: > 0 }`) go
through the class too. Where no model can be asked, a
creation is known by the name it writes and a call by `ContainsKey` or `TryGetValue`, the two names
only a dictionary answers.

### The wire keeps working, and says what it cannot keep

Every dictionary field and Server Action result now carries `{ dict, key }` in its hydration spec,
where `key` names how a JSON property name becomes the key: a number, a long, a decimal, a date, or
the text itself. The class writes itself with `toJSON` as the JSON object System.Text.Json reads. A
JSON object cannot carry an order for integer-like names (`JSON.parse` sorts them), so an integer
keyed dictionary that crosses the wire arrives ascending; keeping its order needs a wire format of
its own, which is #437.

### The DOM escape hatch reads either form

`HtmlElement.BuildAttributes` and `BuildEvents` answer the class, since their C# signatures say
`Dictionary` and only a consumer's own subclass calls them. The renderer, `DynamicElement`,
analytics and route values read a bag through one helper that walks a dictionary or a plain object,
because the runtime's own nodes keep plain objects on the hot path.

### TypeScript annotations

A dictionary member annotated `Record<string, any>` would not type-check against the class, and an
`IDictionary` already degraded to `any`. Every dictionary annotation degrades to `any` alike, an empty
local included, which TypeScript would otherwise infer keyed and valued by `unknown`. Reading one
goes through `$eq.mapGet`, whose value type defaults to `any`: inference from an `any` map finds no
candidate and would land on `unknown`, refusing every read in a twin, while a typed map still infers
its value.

## Risks / Trade-offs

- [A lookup by value is a linear scan] → As the record-keyed dictionary always was; a key found by
  identity is a `Map` lookup.
- [Hand-written JavaScript that indexed a transpiled dictionary as an object breaks] → Nothing in
  this repository does after this change; the migration line says what to write instead.

## Not here

- The JSON wire's order for integer keys (#437).
- `HashSet<T>` reuses a removed slot the same way in .NET, and a JavaScript `Set` does not (#438).
- LINQ over a dictionary as a sequence of pairs (`d.Where`, `d.Select`, `d.First()`) still reaches
  array templates that a dictionary does not answer, as it did as a plain object (#439).
- `Add` of a key already there replaces its value where .NET throws, as it did (#440).
- `string.Join` writes each element with JavaScript's `toString`, so `string.Join(",", d.Keys)` over
  bool keys answers `true,false` (#441).
- EqJson refuses a dictionary keyed by an enum in both directions, measured on .NET 10, and a flags
  enum's key crosses as member names (#442).
- A dictionary keyed by an enum with aliases compares the member names, and `ToDictionary` keeps
  refusing one.
