## Why

A sorted dictionary's keys answered `Contains` through no translation: `SortedDictionary<TKey,
TValue>.KeyCollection` declares its own `Contains`, which C# binds before the interface's, and the
transpiler mapped only `Dictionary<TKey, TValue>.KeyCollection`'s. A page asking `d.Keys.Contains(k)` of
a sorted dictionary failed the build with EQ2004, although the runtime's view, live since #864, answers
it (#928). `main` fails the conformance case #919 added for it since #914 made a refused build fail its
case.

## What Changes

- `SortedDictionary<TKey, TValue>.KeyCollection.Contains` lowers to the view's `contains`, as a
  dictionary's keys' does.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `runtime-dictionaries`: a sorted dictionary's `Keys.Contains` answers as .NET's.

## Impact

`BclSurfaceTailStrategy` alone. No public API, no developer surface, no runtime change.
