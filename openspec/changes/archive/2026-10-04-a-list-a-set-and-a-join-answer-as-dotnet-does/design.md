# Design

## One equality, chosen from the type

.NET decides every search below by `EqualityComparer<T>.Default`, and the browser had three answers:
`indexOf`'s `===` for a list's search, `$eq.equals` for `Contains` over a value type, the key table of
the dictionary for a key. The compiler now asks one place, `ElementEquality`, which writes the
runtime's `KeyEquality`: identity (nothing passed), value (`true`), the value's own (`'own'`), or a
comparison generated from the members' types for a tuple, an anonymous type or a pair whose members
`$eq.equals` would compare otherwise (an array, a class without `Equals`). Only the static type can
say which member of a tuple is an array: both are arrays here. The runtime keeps the same vocabulary
in `utils/key-equality.ts`, and memoizes a generated comparison by its shape, so two sets of one
element type hold one comparer, which .NET's set operations ask before they take their fast paths.

## One slot table for a dictionary and a set

.NET's `Dictionary` and `HashSet` hold their entries the same way: by slot, the last freed slot
taken first, enumeration walking the slots. The runtime's dictionary already did; the set is built on
the same table (`utils/slots.ts`), into which the dictionary moved, so the two cannot drift. The table
keeps .NET's capacity too (its prime table and growth), because two observable things depend on it: a
copy of a set keeps the set's free slots while the source is not much larger than it holds
(`ConstructFrom`), and `TrimExcess` packs only past the capacity the set needs. A key is found through
a `Map` by identity, or by a walk with the comparison; #550's `$eq.hash` can bucket the walk later,
in the table, for both collections at once.

The set has a `Set`'s surface (`add`, `has`, `delete`, `size`, the iterators), so the TypeScript
annotations that read `Set<T>` and the runtime's own readers keep working, and .NET's members by their
camelCase names, each ported from .NET's source step for step.

## .NET's sort, both helpers

A sort that is stable cannot be .NET's: .NET's introspective sort moves equal elements, and a page
that sorts by a key shows them in .NET's order. `utils/sort.ts` ports both of .NET's helpers:
`ArraySortHelper<T>` for a comparison (its partition reads unchecked, which is how .NET detects an
inconsistent comparer) and `GenericArraySortHelper<T>` for the default comparer of an `IComparable<T>`
(null-aware, a double's NaNs moved to the front first). The compiler picks the helper and the order
from the type (`SortOrders`, reading `ValueOrdering`, the table `Max`, `Min` and the sorted collections
read). A traced comparison sequence on an input built to reach the heap sort is the same on both
sides, which is the proof the port is the algorithm and not an algorithm.

## A join writes text as a concatenation does

`StringConversion` split into the syntax rule a concatenation applies (a literal null, an enum member
written by name) and the type rule beneath it, which `string.Join` applies to each element: the
runtime's `join` reads any sequence by its iterator and is handed the conversion only where
JavaScript's own string of the element type differs from .NET's.
