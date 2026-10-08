# Design

## How Flutter answers it

Flutter has no server rendering and no JSON boundary of its own, so `docs/FLUTTER-PARITY.md` has no
row for this. Dart's `jsonDecode` builds a `LinkedHashMap`, which keeps the order of the text for
every key. A JavaScript object does not, and `JSON.parse` offers no other container, so the order has
to travel in a shape JavaScript keeps: an array.

## Why the pairs, and not a reviver, an order sidecar or a parser

The order is lost in three places, and none of them can be reached afterwards:

- `JSON.parse` and `response.json()` reorder the object before a reviver is called for it.
- The page's state is written into the page as a JavaScript object literal
  (`window.__INITIAL_STATE__ = {…}`), which the engine builds with no hook at all.
- The browser's own `toJSON` builds an object before `JSON.stringify` writes it.

A sidecar holding the order (`{"$order": [3, 1], "3": …}`) would need both sides to agree on a
reserved name a key may also have, and a JSON parser of our own would replace the engine's native
one on every payload. An array keeps its order everywhere, and a set already crosses as one. A
dictionary's array is its pairs, `[[key, value], …]`, which is also how `new Dictionary(pairs)`
takes it in the runtime.

## A key is written as a value

Inside an array, a key is a value, so it is written by the converter of its type, as every value of
that type is: a long and a decimal as their text, an enum as its camelCase name, a `[Flags]` enum as
its number, a date as its ISO text, a bool as `true` or `false`. The browser revives it by the tag
the hydration spec already carries (`key`), with one rule for keys and values. A bool key, which the
property name wrote `"True"`, is the visible change.

## Which types cross as pairs

The converter matches, by generic definition, the five shapes the browser holds as its class
(`BoundaryShape.DictionaryName`: `Dictionary`, `IDictionary`, `IReadOnlyDictionary`,
`SortedDictionary`, `SortedList`) and `ReadOnlyDictionary`, the read-only view of one. Matching every
type that implements `IDictionary<,>` would also take an `ExpandoObject` and a `JsonObject`, which
are objects by meaning. A dictionary-like type outside the six is written as System.Text.Json writes
it, an object: a `FrozenDictionary`, an `ImmutableDictionary` and a `ConcurrentDictionary` have no
order .NET defines, and hydration still builds the class from the object.

The converter enumerates the value it is given, so a member declared `IReadOnlyDictionary` crosses
as pairs whatever implements it.

## The server's own maps

The page's state is a map of components to maps of fields, and a projected service is a map of
member names. Those are objects by meaning, and the browser reads them by name. The state payload is
written as objects by name, field by field, each value through `EqJson`, and a projected value's
members are a `ProjectedMembers`, a type of their own that the converter does not match.

## Reads stay lenient

`EqJson` reads a long from text or a number. The dictionary converter reads the pairs or an object,
and hydration reads either, since the server still writes an object for the types outside the six.
Duplicate keys in the pairs keep the last value, as System.Text.Json does for a repeated property
name.

## The proof

- The payload fixture the server writes and the runtime reads (`ServerPayloadFixtureTests`,
  `hydrate.spec.ts`) holds 3 before 1 and reads back 3, 1.
- `EqJsonTests` pins the pairs written in slot order for integer and integer-like string keys, each
  key type, both reads, and the state payload's objects.
- The conformance suite's `Json_MatchesDotNet` gets integer and integer-like keys, executed on both
  sides, once `RuntimeJson` writes a dictionary as the runtime does, and a crossing page renders
  integer and integer-like keys on the server and builds its twin in Bun on that payload.
- A Server Action's request is pinned in one file, `action-arguments.json`: the runtime's spec writes
  those bytes for its two dictionaries, and the server's test posts them to the middleware and reads
  the keys back in the browser's order. Its answer is checked as the pairs, in order.
