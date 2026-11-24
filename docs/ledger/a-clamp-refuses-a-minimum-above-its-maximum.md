- **2026-11-24 · A number's Clamp refuses a minimum above its maximum**: a Bug under #1587
  ([#1824](https://github.com/eQuantic/equantic-ui/issues/1824)). `Math.Clamp` over a number other than a decimal
  answered the maximum where .NET refuses a minimum above it (`Math.Clamp(2.0, 3.0, 1.0)` was 1), and the
  `Math.min(Math.max(…))` it was written as turned a NaN bound into NaN and dropped a negative zero's sign. Every
  number's `Clamp` and `ClampNative` now go to the runtime's `clamp` and `clampLong`, which refuse with .NET's
  `ArgumentException`, the values written in the culture as their type writes them, and compare in .NET's order.
  Measured on both sides by `NumberClampConformanceTests`. Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-bcl`).
