# Proposal

Part of #516 and #494, under #522's review: the second round of the pull request found that a sorted
collection the hydration rebuilds does not keep the server's order, and that the guard keeping a
foreign comparer out of the page read the wrong member.

## Why

- Every sorted collection the browser built or rebuilt ordered by `<`. A `SortedSet<string>` put `B`
  before `a`, where .NET's `Comparer<string>.Default` follows the current culture; one of decimals
  compared their text, putting `10` before `9` and keeping `1.0` beside `1.00`, which .NET finds
  equal; a NaN equalled every number; and `new SortedSet<string>(StringComparer.Ordinal)` handed the
  comparer to the factory as the elements to copy, which threw. A hydrated `SortedSet` therefore
  enumerated in another order than the server's, or lost elements.
- The comparer guard read any public `Comparer` property, so a plain object with one was left out of
  the page, while an `ImmutableHashSet` (whose comparer is `KeyComparer`), a `ReadOnlyDictionary`
  wrapping a case-insensitive dictionary and a set of the app's own crossed with an equality the
  browser does not keep. A projected value lost every read when one of them was such a collection.

## What Changes

- One table says how a type orders, as `Comparer<T>.Default` does: by value, as a real with its NaN
  first, as text in the current culture, or by the value's own `compareTo`. `Max`/`Min`, every sorted
  collection eqc builds and every one a hydration rebuilds read it. `StringComparer.Ordinal` handed to
  a `SortedSet` asks for the code-unit order.
- The guard reads the comparer of the collections whose comparer can be read (each by its member,
  wrappers through what they wrap), and refuses a set or a dictionary of any other class in a member
  the browser rebuilds as one. A sorted one keeps only its element type's default order, so an ordinal
  comparer is foreign to it. A projected value loses only the read that ends at such a collection.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-bcl`: a sorted collection orders as its element type does.
- `hydration-contract`: the collection requirement says which comparers cross, and that a projected
  value loses only the read.

## Impact

- **eqc**: the ordering table (`ValueOrdering`), `Max`/`Min`, the sorted set, sorted dictionary and
  sorted list constructions, and the hydration spec.
- **Runtime**: `utils/ordering.ts`, the sorted factories, hydration.
- **Server**: the comparer guard and the projection.
- **Break**: a sorted collection of strings built in the browser orders in the current culture, as
  .NET does, where it ordered by code unit.
