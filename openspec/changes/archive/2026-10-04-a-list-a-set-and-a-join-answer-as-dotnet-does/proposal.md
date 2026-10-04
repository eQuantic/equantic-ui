# Proposal

Closes #488, #425, #438, #531, #429 and #441, sub-issues of #164.

## Why

A list, a set and a join answered as the JavaScript method of a similar name answers, which is not
what .NET answers. Measured through the conformance runner, both sides executed, on main (16094a9e):

- `List<T>.Sort()` compared the elements' text (`10, 9, 1` sorted to `1, 10, 9`) and was stable where
  .NET's introspective sort is not; `RemoveAll` threw `ReferenceError: _removed is not defined`;
  `BinarySearch` was a `findIndex` that answered -1 for every miss; `Sort(StringComparer.Ordinal)`
  named a class nothing defines; `FindIndex(1, x => x > 4)` handed `findIndex` the index for its
  predicate; `CopyTo(a)` built a copy nothing read; `Find` answered `undefined` for no match (#488).
- `IndexOf` and `LastIndexOf` compared with `===`, so a NaN, a record or a tuple equal to one in the
  list was never found, and `Contains` and `IndexOf` disagreed about one list; a tuple holding an
  array compared the array element by element, where `ValueTuple.Equals` compares it by reference
  (#425).
- A `HashSet<T>` was a JavaScript `Set`, which found a date, a decimal, a tuple and a record by
  reference (#531) and appended where .NET reuses the slot a removal freed (#438).
- `string.Join` called `join` on its values, which a set and a linked list do not have (#429), wrote
  each value by JavaScript's `toString` (`true`, an enum's key, a float's double digits) (#441), and
  the values a params array takes one by one became `1.join(',')`, which does not parse.

## What Changes

For a developer, the C# is the same and the browser now answers what .NET answers:

- A `List<T>` and an `Array` sort by .NET's introspective sort, unstable as .NET's is, through the
  type's default comparer, a `Comparison<T>`, a `StringComparer`'s six, `Comparer<T>.Create` or a
  comparer the app writes, with .NET's two exceptions. `BinarySearch` answers the complement of the
  insertion point. `Find` and `FindLast` answer the element type's default; `FindIndex`,
  `FindLastIndex`, `IndexOf` and `LastIndexOf` take their ranges and check them in .NET's words;
  `RemoveAll` answers how many it removed; `CopyTo` writes into the array it is handed.
- Every search `EqualityComparer<T>.Default` decides in .NET (a list's and an array's `IndexOf`,
  `LastIndexOf`, `Contains` and `Remove`, LINQ's `Contains`, a set's elements, a dictionary's keys)
  compares by ONE equality the compiler chooses from the element type. A tuple, an anonymous type or
  a pair with a member that compares otherwise gets a comparison generated from its member types.
- A `HashSet<T>`, an `ISet<T>` and an `IReadOnlySet<T>` are the runtime's set, wherever one is made
  (a constructor, an initializer, a collection expression, `ToHashSet`, the hydration): its elements
  held by slot in the table the runtime's dictionary keeps its keys in, found by the element type's
  equality, with .NET's members step for step.
- `string.Join` reads any sequence and writes each value as .NET's `ToString` writes it, a null as
  nothing; values passed one by one are each written by their own type; a null separator writes
  nothing.
- A comparer with no form on this side, handed to a sort, a search or `ToHashSet`, is EQ2007, and
  `HashSet.TryGetValue`, whose value arrives through an `out`, is EQ1004, where each emitted a call
  that threw in the browser.
- **BREAKING** for code reaching under the lowering: a set is no longer an `instanceof Set`, which no
  C# can observe; JavaScript that receives one from a component reads it through its iterator or
  `has`, as before.

## Capabilities

### New Capabilities

- `runtime-sets`: what a `HashSet<T>` answers on the web: its slots, its equality, its members, the
  wire.

### Modified Capabilities

- `runtime-dictionaries`: a tuple key holding an array compares the array by reference, by the
  equality a set's elements take too.
- `transpiler-bcl`: a list's and an array's searches, sorts, finds and copies, and `string.Join`.

## Impact

- **eqc**: `ElementEquality`, `SortOrders`, `ParameterTemplate` and `ReflectionName` (new);
  `HashSetStrategy` (now IR), `ListMethodStrategy`, `ArrayStaticStrategy`, `ContainsStrategy`,
  `StringStaticStrategy`, `StringConversion`, `CollectionExpressionStrategy`, `LinqTableStrategy`,
  `ObjectCreationStrategy`, `DictionaryStrategy`, `HydrationSpec`, `CollectionComparerExtensions`; the
  `Eq` constants of the new runtime helpers. The public surface moves (`HashSetStrategy.Convert` goes
  for `ConvertIr`, new `Eq` constants), declared in `PublicAPI.Unshipped.txt`; the developer surface
  does not.
- **Runtime**: `utils/key-equality.ts`, `utils/slots.ts`, `utils/hash-set.ts`, `utils/sort.ts` and
  `utils/list.ts` (new); the dictionary on the shared slot table; `contains`, `remove` and `setAdd`;
  `join`; the hydration of a set. The served runtime grows by about 8 KB gzipped, recorded in its
  budget.
- **The transpiled library**: the code languages' keyword tables and the editor's sets are the
  runtime's set, and their joins go through the runtime.
- **Docs**: the wiki's SupportedFeatures page (EN + pt-BR), `docs/LEDGER.md`, `docs/DIAGNOSTICS.md`.
