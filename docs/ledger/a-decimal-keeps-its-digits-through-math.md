- **2026-11-23 · A decimal keeps its digits on the way to an integer, and Math over it answers**: two Bugs under #1587
  ([#1766](https://github.com/eQuantic/equantic-ui/issues/1766), [#1767](https://github.com/eQuantic/equantic-ui/issues/1767)).
  A decimal cast to an integer went through its double before it was cut, so a long past 2^53 lost its last digits
  and `(int)0.99999999999999999m` was 1; it is now cut toward zero from the decimal's own digits and checked against
  the type's range in .NET's words, for every integer type and a char, and `decimal.ToInt32` and its siblings with it.
  `Math.Abs`, `Max`, `Min`, `Sign` and `Clamp` over a decimal went to JavaScript's `Math`, which read the runtime's
  Decimal as NaN; the numeric table's decimal row answers them with the scale .NET keeps, `decimal`'s statics reach
  it, `Clamp` refuses a minimum above its maximum, and a `Math` member over a decimal with no row fails the build.
  Measured on both sides by `DecimalMathConformanceTests`. Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-bcl`).
