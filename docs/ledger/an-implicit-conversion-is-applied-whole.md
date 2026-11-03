- **2026-11-03 · An implicit conversion is applied whole, and a native integer is 64 bits**: bugs under #849
  ([#649](https://github.com/eQuantic/equantic-ui/issues/649),
  [#911](https://github.com/eQuantic/equantic-ui/issues/911),
  [#970](https://github.com/eQuantic/equantic-ui/issues/970)). A user-defined conversion and the standard one beside
  it are two conversions on one node of the bound tree, and `ValueFlow` applied only the one around the node, so
  `long l = new Count(5)` kept a number; it applies each now, inner first, and asks the pair of types for the
  standard conversions a tuple's element has no tree to show. A tuple converted to wider elements passed its array
  through; each element converts now. An `nint` was a number or a BigInt by where it came from; it is a BigInt,
  64 bits wide as on every target the SDK ships to. Proved by the conformance suite on both sides. Proposed and
  archived through OpenSpec (`transpiler-expressions`).
