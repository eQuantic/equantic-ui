# Proposal

Closes #437, a Bug under #164 (The transpiler's fences hold on every path).

## Why

A dictionary crosses the JSON wire as a JSON object, and a JavaScript object lists every
integer-like property name first, ascending, whatever order the text had. #435 made every
dictionary the runtime's class, which enumerates by slot as .NET's does, but the order is gone
before that class is built:

- A `Dictionary<int, string>` holding 3, then 1 in server-rendered state reaches the browser
  enumerating 1, 3 where .NET enumerated 3, 1. `hydrate.spec.ts` pins it on the payload the server
  writes (`expect(scores.keys()).toEqual([1, 3])`).
- A Server Action's answer and a server topic's payload are parsed by `response.json()` and
  `JSON.parse`, and the page's state is written into the page as a JavaScript object literal, so no
  reviver ever sees the order.
- A dictionary sent to the server is reordered by the browser itself: its `toJSON` builds an object,
  and `dictionary.spec.ts` pins `{"3":2,"b":1,…}` for a dictionary that held `b` first.

The keys affected are the ones JavaScript reads as array indices: every non-negative integer up to
4294967294, an integral double, a long in that range, a digit char and a digit-only string.

## What Changes

- **A dictionary crosses as its pairs**, a JSON array of `[key, value]` arrays in the order it
  enumerates, in both directions: server-rendered state, a navigation's state, a Server Action's
  answer and arguments, and a topic's payload. A key is written as a value of its type is, so a bool
  key is `true` where it was the property name `"True"`, a long, a decimal and a date are their text,
  an enum is its camelCase name and a `[Flags]` enum its number.
- **The server writes the pairs** for the dictionary types the browser holds as its class
  (`Dictionary`, `IDictionary`, `IReadOnlyDictionary`, `SortedDictionary` and `SortedList`) and for
  `ReadOnlyDictionary`, the view `AsReadOnly()` answers, and reads them back. It still reads a JSON
  object for any of them, as it reads a long from text or a number.
- **The browser writes and reads the pairs**: `Dictionary` and `SortedMap` write them with `toJSON`,
  and hydration builds the class from them. Hydration still reads a JSON object, which the server
  writes for a dictionary-like type outside those six (a `FrozenDictionary`, an
  `ImmutableDictionary`, a `ConcurrentDictionary`, a subclass).
- **The server's own maps stay objects**: the state of a page by component, a component's fields and
  the members of a projected service are written by name, and only the values inside them go
  through the dictionary rule.

## Parts reached

- The runtime: `utils/dictionary.ts`, `utils/sorted.ts` and `utils/hydrate.ts`.
- The server: `Json/EqJson.cs`, the state payload in `Rendering/ServerRenderingService.cs`, and
  `Rendering/HydrationProjection.cs`.
- The conformance harness, whose `RuntimeJson` writes a .NET value as the runtime's `toJSON` does.
- eqc does not change: the hydration spec already says which values are dictionaries and how their
  keys revive. Neither the public surface nor the developer surface moves.

## The break

The JSON shape of a dictionary on the SDK's own wire changes from an object to an array of pairs.
Inside the SDK nothing needs to change. A client outside the SDK that calls a Server Action by hand
and reads a dictionary from its answer reads `[[key, value], …]` now: read the pairs, or answer a
record whose members are named.
