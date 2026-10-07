# Proposal

Closes #597, #463 and #554, sub-issues of #565 (the transpiler's fences hold on every path).

## Why

- The runtime's `Queue`, `Stack`, `LinkedList` and `SortedSet` had no `toJSON`, so `JSON.stringify` wrote a queue as `{"items":[...]}` and a linked list as its node graph: a Server Action argument reached the server as an object no collection is read from (#597).
- A dictionary's `Keys` and `Values` were arrays copied when they were read, where .NET's are live views. `EnsureCapacity` answered the capacity asked for, where .NET answers the prime it settles on, and `TrimExcess` did nothing, so the next key went into a freed slot instead of after the others (#463).
- A record's twin compared every member with `$eq.equals`, which walks an array element by element, so two records holding two arrays of the same items were equal where .NET compares the arrays by reference. A tuple's `Equals` did the same (#554).

## What Changes

- **Each collection writes the JSON .NET writes.** `Queue`, `Stack`, `LinkedList` and `SortedSet` write an array in the order each enumerates, a stack from the top.
- **A dictionary's views are live, and its capacity is .NET's.** `Keys` and `Values` read the dictionary when they are read, an array to everything a lowering hands them to, through a Proxy over a frozen snapshot taken again only when the dictionary changed. A key added while they are walked ends the walk with .NET's InvalidOperationException, and the keys' own `Contains` finds a key as the dictionary does. `EnsureCapacity` and `TrimExcess` keep .NET's prime capacity, and `new Dictionary<K, V>(capacity)` hands the factory its capacity.
- **A record, a struct and a tuple compare each member by its type's default comparer.** `ElementEquality.Compare`, the comparer `EqualityComparer<T>.Default` stands for written as JavaScript, compares a record's members and a tuple's `Equals`: an array and a class by reference or their own `Equals`, a tuple holding an array by the comparison generated from its element types, and everything `$eq.equals` already answers right through it. A tuple seen only as `object` is an array here and keeps comparing by its elements, which the wiki says.

For a developer using the SDK: a `Queue` or a `SortedSet` reaches a Server Action as the collection, `var ks = d.Keys;` sees the keys added afterwards, and `record R(int[] Items)` compares as it does in .NET. Nothing an app writes changes. The public surface gains `Eq.SameKey` in eqc's `PublicAPI.Unshipped.txt`, and the developer surface does not move.

## Capabilities

### New Capabilities

- `runtime-collections`: what the runtime's `Queue`, `Stack`, `LinkedList` and `SortedSet` write to JSON.

### Modified Capabilities

- `runtime-dictionaries`: live views and .NET's capacity.
- `transpiler-records`: a member compared by its type's default comparer.

## Impact

- The runtime: `utils/collections.ts`, `utils/sorted.ts`, `utils/dictionary.ts`.
- eqc: `ElementEquality.Compare`, `RecordTypeEmitter` (a member's equality), `StructuralEqualsStrategy` (a tuple's `Equals`), `DictionaryStrategy` (a capacity), `BclSurfaceTailStrategy` (`EnsureCapacity`, `TrimExcess` and the keys' `Contains`).
- Tests: `CollectionJsonConformanceTests`, `DictionaryViewConformanceTests` and `ArrayMemberEqualityConformanceTests`, both sides.
