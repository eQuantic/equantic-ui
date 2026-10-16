# Proposal

Closes #913 and #922, two Bugs under #849 (The transpiler's fences hold on every path, continued), found
by the second round of review on #906.

## Why

A LINQ operator that holds its keys or its elements does it in a JavaScript `Map` or `Set`, which find
a key by reference, and `Distinct` found a record or a struct by the text `JSON.stringify` writes of it.
.NET finds both as `EqualityComparer<T>.Default` does. At dfce1158, 19 of the 21 cases of the rows this
change adds fail:

- a join by an anonymous object or a tuple key matches nothing, and `Union`, `Intersect`, `Except` and the
  `By` forms keep every anonymous object and every tuple (#922);
- over a record, a decimal or a date the set operators and a join compare by reference, and `Distinct`
  finds two records holding different arrays of the same items equal;
- a class that overrides `Equals` is never asked it, nor is the `Equals` a subclass overrides behind its
  base or an interface;
- `GroupBy` and `ToLookup` by a tuple or an anonymous object holding an array walk the array, and a
  `GroupBy` over doubles makes each NaN a group of its own;
- the `ToString` method group of an array, a list, a dictionary, a set or a delegate writes JavaScript's
  text, and an enum's writes its key (#913), where the call writes the type's name and the member's.

The two that pass are what already answered as .NET: a null array's `ToString` group refused where the
delegate is made, and a dictionary, `Contains` and `IndexOf` keyed by a tuple holding an array, which
`ElementEquality` decides since #425 and #554.

## What Changes

- Every LINQ operator that holds a key or an element reads the decision `ElementEquality` makes for a
  dictionary, a set, `Contains` and `IndexOf`: `Join` and `GroupJoin` by their key, `Union`, `Intersect`,
  `Except` and `Distinct` by their element, and `DistinctBy`, `UnionBy`, `IntersectBy` and `ExceptBy` by
  their key hold them in the runtime's dictionary and set, and `GroupBy`, `ToLookup` and a lookup's
  indexer compare a key by the comparison it writes. A type compared by identity keeps the JavaScript
  `Map`, `Set` and `===`, which answer as .NET does, except a float or a double, whose NaN a group finds
  equal to itself.
- The `ToString` method group of a value whose type writes its text writes what the call writes, of the
  value as it is when the delegate is made, read once there and refused there when it is null.

A developer writes nothing new: the C# they already wrote answers in the browser what it answers on the
server.

## Impact

- eqc: `LinqTableStrategy` holds a join's and a set operator's keys in the runtime's collections by the
  key type's equality, `DistinctStrategy` reads `ElementEquality` and loses its `JSON.stringify` filter,
  `LinqKeys.Matches` writes the key type's comparison for `GroupBy`, `ToLookup` and the lookup indexer,
  and `MemberAccessStrategy` makes every `ToString` method group through `StringConversion.TextGroup`.
- The runtime: `$eq.text.group` (`utils/format.ts`).
- The shared components' modules: `BoundReceivers` finds its distinct pairs in the runtime's set.
- Public surface: `Eq.TextGroup` is added. The developer surface does not move.
- Not reached: a comparer handed to an operator is still the collection fence's to judge (#578).
