# Proposal

Closes #564, #578, #585, #586 and #593, sub-issues of #565 (the transpiler's fences hold on every
path, continued). Each is a collection or an indexer that answers in the browser differently from
.NET, with a green build.

## Why

Measured on main (fc8f0fdc), both sides executed by the conformance suite:

- A list built with a constructor argument and a collection initializer takes one or the other
  (#564): `new List<int>(10) { 1, 2 }` is `[]`, and `new List<int>(source) { 3 }` is
  `source, [3]`, a second declarator in a declaration and a second argument in a call. The
  target-typed form keeps the elements and drops the source, and `List<int> c = new(source)` is empty.
- `ToDictionary` refuses every comparer, `StringComparer.Ordinal` included, which the comparer fence
  every collection's construction passes lets through (#578). `GroupBy` refuses every one too,
  `ToLookup(k, comparer)` calls the comparer as an element selector, and `Distinct` drops whatever
  comparer it is handed, so `Distinct(StringComparer.OrdinalIgnoreCase)` keeps "a" and "A".
- A range over a type with a `Length` and a `Slice(int start, int length)` calls JavaScript's
  `slice(start, end)` on the twin, whose `slice` is that `Slice` (#585): `new Strip()[1..3]` is two
  elements in .NET and three in the browser, and `[3..1]` slices where .NET's `Slice(3, -2)` throws.
- A twin read through `IReadOnlyList<T>` or `IList<T>` reads a JavaScript subscript and a `length`,
  which a twin does not answer (#586): `r[0] + r[2] + r.Count` over a twin is 19 in .NET and null in
  the browser.
- `ICollection<T>`'s `Add` and `Clear` are an array's `push` and `splice`, which a `HashSet`, a
  `LinkedList`, a `SortedSet` and a dictionary's pairs behind the interface lack (#593): `c.Add(2)`
  throws `c.push is not a function`.

## What Changes

- A `List<T>` construction is one array, as C# builds it: what its constructor copies, spread, then
  each element of its collection initializer in order; an object initializer is applied to it once
  built. The explicit and the target-typed forms share it.
- `ToDictionary`, `ToLookup`, `GroupBy` and `Distinct` hand their comparer to the fence a collection's
  construction passes (EQ2007): one that asks for the key type's own equality, ordinal strings or null
  is dropped, since the lowering already finds the keys that way, and any other fails the build.
- A range over a type eqc writes calls the member the bound tree names: its `Slice` with the start and
  the length C# computes, its receiver evaluated once, then its endpoints in order, then its `Length` or
  `Count`, read only where an endpoint counts from the end or the end is open. A range handed to an
  indexer that takes the `Range` itself fails the build (EQ2004), since a System.Range value has no
  translation. A range over a string, an array or a list keeps JavaScript's `slice`.
- An access through a list's face (`IList<T>`, `IReadOnlyList<T>`, `IList`) reads and writes through
  the runtime's `$eq.collections.item` and `setItem`, which answer an array's subscript and a twin's
  `item` and `setItem` alike, from the end too, and its `Count` through `$eq.collections.count`, which
  reads a twin's own `count` before a `size` or a `length` it may also declare. A receiver typed as an
  array or a list keeps its subscript.
- `ICollection<T>`'s own `Add` and `Clear` go through the runtime's `$eq.collections.add` and `clear`,
  which add and empty as the collection behind the interface does, a member's collection initializer
  included.
- No break for an app: its C# compiles to the new forms. The public surface of `eQuantic.UI.Compiler`
  gains four `Eq` constants and `RangeIndexerStrategy` moves to the IR (`ConvertIr` in place of
  `Convert`); the developer surface does not move.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-bcl`: a list built with an argument and an initializer, a list's face, and
  `ICollection<T>`'s `Add` and `Clear`.
- `transpiler-sequences`: a LINQ operator handed a comparer.
- `transpiler-expressions`: a range over a type with a `Slice`.

## Impact

- eqc: `ObjectCreationStrategy` (the list), `LinqTableStrategy`, `GroupByStrategy`, `DistinctStrategy`
  and the collection fence (comparers), `RangeIndexerStrategy` and `RangeExpressionStrategy` (ranges),
  `Indexer`, `Place`, `CountSpelling` and `IndexFromEndStrategy` (the list's face), and
  `ListMethodStrategy` and `ObjectInitializer` (`ICollection<T>`).
- The runtime: `add`, `clear`, `item` and `setItem` in `utils/collections.ts`, exposed on
  `$eq.collections`, and `count` reading a twin's `count`. The runtime's transpiled components
  regenerate where they read a list through its face.
- Tests: conformance cases for each issue, both sides executed, the twins through the module graph;
  Compiler tests for the comparers EQ2007 refuses and the range EQ2004 refuses; runtime specs.
- Docs: docs/DIAGNOSTICS.md for EQ2004 and EQ2007, and one docs/LEDGER.md line.
