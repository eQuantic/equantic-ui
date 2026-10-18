# Tasks

## 1. Measure before changing

- [x] 1.1 Write the cases in `NullableStructConformanceTests`, executed on both sides: a struct's own `Value`, `HasValue`, `GetValueOrDefault` and `Count` through `?.` and a property pattern, an empty nullable unwrapped by a cast, a struct's operator, a `foreach` and `Value`, and `Equals`, `Nullable.Equals` and `EqualityComparer<T>.Default.Equals` over a record struct, a struct with its own `==`, one with its own `Equals(object)` and a vocabulary value, with the spreadsheet's `SheetRange?` and `SheetCellSnapshot?` through an app's module graph
- [x] 1.2 Run them on the code before the change: 73 of 95 statement cases fail, and 14 of the 15 module-graph cases in both modes
- [x] 1.3 Read what Roslyn's bound tree says of each shape: the symbol a member binding binds behind `?.`, the explicit nullable conversion nested inside a user-defined one, and the method each `Equals` call binds

## 2. The fixes

- [x] 2.1 `NullableStrategy` answers only the members `Nullable<T>` declares, by the symbol the model binds, and `CountSpelling` reads a count on a nullable value's own type (#966)
- [x] 2.2 `$eq.nullable.value` in the runtime, written by `ValueFlow.Apply` wherever the bound tree unwraps a nullable into a type that is not nullable, by `CastExpressionStrategy`'s enum branches and by `NullableStrategy` for `Value` (#967)
- [x] 2.3 `StructuralEqualsStrategy` lowers `Nullable<T>.Equals(object)` over a struct, `Nullable.Equals<T>` and a call made on `EqualityComparer<T>.Default` as the default comparer's comparison, and the Compiler test pins the build on both sides of the change (#968)
- [x] 2.4 The spreadsheet's fill move writes `return FillTarget != before;`, its transpiled module moves by that comparison alone, and the C# suite and the twin's spec make the moves through an absent preview (#969)
- [x] 2.5 Run the same cases: all 97 rows pass

## 3. Proof and documentation

- [x] 3.1 Run the compiler's and the conformance suite's cases of nullable values, conversions, casts, equality, records, structs, tuples and patterns, the transpiled components' pins, the BCL audit, the runtime's suite and the served runtime's budget
- [x] 3.2 One ledger entry under `docs/ledger/` citing the four issues
