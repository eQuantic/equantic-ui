# Tasks

## 1. Measure before changing

- [x] 1.1 Write the cases in `LiftedOperatorConformanceTests`, executed on both sides: an app's record struct, a struct with its own operators, a conversion, a tuple's element, a `Color?` and the spreadsheet's `SheetRange?` through an app's module graph, each beside an absent value, inside a lambda, a query and a guard
- [x] 1.2 Run them on the code before the change: 28 of 67 statement cases and 3 of 9 module-graph cases, in both modes, fail
- [x] 1.3 Read what Roslyn's bound tree says of each shape: `IsLifted` and `OperatorMethod` on the binary, unary and compound operations, the conversion's method and its two types

## 2. The lift

- [x] 2.1 Read the bound operator once in `BinaryExpressionStrategy` and hand its lift to `UserDefinedOperators`, and send a lifted equality whose operator no twin carries to `$eq.equals`
- [x] 2.2 Lift every call of a struct's own operator and conversion in `UserDefinedOperators`, through `NullableLift`: the equality's lift, the relation's and the arithmetic's, each operand evaluated once, in order
- [x] 2.3 Pass the lift the bound tree reads from the compound, the unary operator, the step, a tuple's element and a user-defined conversion in `ValueFlow`
- [x] 2.4 Run the same cases: all pass, and an operator declared over the nullable values is still called as written

## 3. Proof and documentation

- [x] 3.1 Run the compiler's and the conformance suite's cases of operators, equality, nullable values, records, structs, tuples and conversions, and the transpiled components' pins, which do not move
- [x] 3.2 One ledger entry under `docs/ledger/` citing the issue
