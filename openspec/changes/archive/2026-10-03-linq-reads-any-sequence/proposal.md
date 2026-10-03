# Proposal

Closes #434, #439 and #524, sub-issues of #164.

## Why

Every LINQ operator was lowered as an array method (`filter`, `map`, an index, `length`) on its
receiver as converted, and the browser holds most sequences as something else. Measured through the
conformance runner, both sides executed:

- A `HashSet`, the runtime's sorted set, queue, stack and linked list, and a sorted dictionary or
  list threw on their first operator, `TypeError: s.filter is not a function`, and `queue.First()` was
  claimed by the queue's own lowering, which has no `first` (#434).
- A dictionary read as its pairs (`d.Where(kv => …)`, `d.First()`, `d.Count()`) reached the same array
  templates (#439).
- A string read as a sequence threw on `Count`, `Where`, `Any`, `All` and `Select`, `foreach` and
  `ToCharArray` counted code points where .NET counts code units, `new string(char[])` named a class
  nothing defines, and `"a1b2".Count(char.IsDigit)` was refused with EQ1001 (#524).
- `ToList()` and `ToArray()` handed their source back, so a copy and its source were one array.

## What Changes

- One place reads a LINQ operator's source: an array, a list or an operator's own result as it is,
  anything else through `$eq.linq.seq`, which reads a string by its code units and any other sequence
  by its iterator, and refuses a value that is not one.
- `ToList` and `ToArray`, LINQ's and a BCL collection's own, make a new array, always.
- The runtime's queue and stack enumerate in .NET's order, and a queue's or a stack's own members
  stay theirs while a LINQ operator over them goes to LINQ.
- A string enumerates and splits by code unit, `new string(char[])` and its range overload build their
  text, and a char static handed over as a delegate is an arrow over its parameters, by its type's
  spelling or under `using static System.Char`.

## Capabilities

### New Capabilities

- `transpiler-sequences`: how a sequence crosses, LINQ over any collection, a string's chars, and the
  copy LINQ materializes.

### Modified Capabilities

(none)

## Impact

- **eqc**: `LinqSource` (new), the LINQ strategies, `CollectionMaterializationStrategy`,
  `QueueStackStrategy`, `ForEachStatementStrategy`, `StringMethodStrategy`, `ObjectCreationStrategy`,
  `CharMethodStrategy`, and two `Eq` constants (`LinqSeq`, `LinqToArray`).
- **Runtime**: `seq` and `toArray` in `utils/linq.ts`, iterators on `Queue` and `Stack`.
- **The transpiled library**: the code editor's `CodeDocument`, `CodeLineCells` and `CodeRows` pins,
  a copy where they call `ToList` and a line's chars by code unit.
- **Break**: a string's `foreach` and `ToCharArray` give UTF-16 code units, so a surrogate pair is two
  chars, as in .NET, where it was one.
