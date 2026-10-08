# Tasks

## 1. A list built with an argument and an initializer (#564)

- [x] 1.1 One array for a `List<T>` construction, the explicit and the target-typed forms alike: the
  source copied or a capacity ignored, then the initializer's elements, an object initializer applied
  once built. Verified by conformance cases (a capacity, a source, the target-typed forms, an argument
  position, a set, a dictionary and a string as the source, the order), failing on main and green here

## 2. A LINQ operator's comparer (#578)

- [x] 2.1 `ToDictionary` and `ToLookup` judge their key comparer by the collection fence and take the
  shape of the arguments left, named ones included; `GroupBy` and `Distinct` judge theirs by the same
  fence. Verified by conformance cases for each operator and shape, failing on main and green here, and
  by Compiler tests for the comparers EQ2007 refuses and the ones it passes

## 3. A range over a type with a Slice (#585)

- [x] 3.1 `RangeIndexerStrategy` on the IR, reading the member the bound tree names: a twin's `Slice`
  with the start and the length C# computes, in .NET's order, and EQ2004 for an indexer over `Range`.
  Verified by conformance cases through the module graph (the row of #585, from the end, open ends, a
  zero from the end, named endpoints, the order measured on .NET, a reversed range, a type that slices
  into itself, a string, an array and a list), failing on main and green here, and a Compiler test for
  the refusal

## 4. A list's face (#586)

- [x] 4.1 The runtime's `item` and `setItem`, and `count` reading a twin's own `count` before a `size` or
  a `length` beside it; the `Place` of a list face's indexer reads and writes through them, from the end
  too, and a `Count` through the face counts through the runtime. Verified by conformance cases through
  the module graph (the row of #586, from the end, a parameter, a write, a compound, a step, the order of
  a write and of a compound, a null-conditional read, an object initializer's entry, an array and a list
  behind the faces, a twin with a `Length` and a `Size` beside its `Count`), failing on main and green
  here, and by runtime specs

## 5. ICollection's Add and Clear (#593)

- [x] 5.1 The runtime's `add` and `clear`, and the lowering of `ICollection<T>`'s own `Add` and `Clear`
  through them, a member's collection initializer included. Verified by conformance cases (a set, a
  linked list, a sorted set, a dictionary's pairs, a sorted dictionary and list, a list and a set
  behind their faces, a class of the app's own, a member's initializer), failing on main and green
  here, and by runtime specs

## 6. The real thing

- [x] 6.1 Regenerate the runtime's transpiled components (`EQ_UPDATE_TRANSPILED=1`) and run the Runtime
  suite (`-t:TestRuntime`), the Server suite (the served runtime's budget), the Web suite, the Compiler
  suite and the Conformance suite

## 7. Documentation and archive

- [x] 7.1 docs/DIAGNOSTICS.md for EQ2004 (a range handed to an indexer over `Range`) and EQ2007 (a LINQ
  operator's comparer), the wiki's Supported Features, Compiler and Diagnostics pages in English and
  Portuguese, on the wiki branch of the same name, and one docs/LEDGER.md line citing the five issues
  with the A/B counts
- [x] 7.2 `openspec archive a-collection-and-an-indexer-answer-as-dotnet-does --yes`
