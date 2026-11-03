## 1. A native integer

- [x] 1.1 Treat `nint` and `nuint` as the 64-bit integers they are wherever the compiler decides by width: `IntegerWidth`, literals, defaults, ordering, hydration, the TypeScript type, `MinValue`, `MaxValue` and `Math`.

## 2. A conversion applied whole

- [x] 2.1 Settle every implicit conversion around a node in `ValueFlow`, and the standard conversions beside a user-defined one from the pair of types.
- [x] 2.2 Convert a tuple element by element (`TupleElements`), in a checked context too, and let an explicit cast of the two-conversion shape take what its operand settled.

## 3. Tests

- [x] 3.1 Conformance tests (`NativeIntegerConformanceTests`, `ConversionChainConformanceTests`): each row of the three issues, every operator of a native integer, each element type of a tuple, at an assignment, an argument, a return and a branch, both sides executed.

## 4. Documentation

- [x] 4.1 The wiki's SupportedFeatures page, in every language it is written in.
- [x] 4.2 A ledger entry under `docs/ledger/`.
