# Proposal

Closes #440, #395 and #433, sub-issues of #164 (the transpiler's fences hold on every path).

## Why

- A dictionary's `Add` lowered to the class's `set`, which replaces the value, and the constructor
  seeded through the same `set`: `Add`, a collection initializer `{ { "a", 1 }, { "a", 2 } }` and the
  constructor that copies pairs all kept the last value, where .NET refuses the second key with an
  `ArgumentException` (#440). It was what #395 still held open once #443 made every dictionary the
  runtime's class.
- `new KeyValuePair<K, V>(key, value)` named a class nothing defines, so a pair built by the app threw,
  where one a dictionary yielded worked (#433).

## What Changes

- `Add`, a collection initializer and the constructor refuse a key already there in .NET's words,
  each collection's own: a `Dictionary` names the key, a `SortedDictionary` the pair it was handed, and
  a `SortedList` the key and its parameter. The indexer's write, an object initializer's
  `[key] = value` and `TryAdd` keep replacing or declining.
- `new KeyValuePair<K, V>(key, value)`, `KeyValuePair.Create(key, value)` and the parameterless
  constructor build the pair a dictionary yields, which destructures and reads `.Key` and `.Value`, and
  `default(KeyValuePair<K, V>)` is the pair of the two defaults.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `runtime-dictionaries`: `Add` refuses a key already there, and a pair built by the app is the pair a
  dictionary yields.

## Impact

- **eqc**: `DictionaryStrategy` (`Add`, the two initializer forms), a new `KeyValuePairStrategy`,
  `DefaultValue`, and `Eq.Pair`.
- **Runtime**: `Dictionary` and `SortedMap` gain an `add` that answers the dictionary, so an
  initializer chains one call per entry (`add`, or the indexer's existing `set`), their constructors
  add, a sorted one knows which of the two it is through hydration, and `$eq.collections.pair` is
  exported.
- **Break**: a dictionary built with a key twice by `Add`, a collection initializer or the pairs
  constructor now throws, as it does on the server.
