- **2026-10-30 · A number narrowed to an integer answers .NET's value**: bugs under #849
  ([#401](https://github.com/eQuantic/equantic-ui/issues/401),
  [#1015](https://github.com/eQuantic/equantic-ui/issues/1015)). `Convert.ToInt32` and its siblings of anything
  but text were `$eq.math.round`, with no range, no BigInt, a decimal handed back and a char read as a number;
  they go by the overload C# bound now, through the runtime's `utils/number-convert.ts`, in .NET's words, and an
  `object` is read by what it holds when the call runs. An explicit cast of a double or a float wrapped where
  .NET 9 and later saturate; it saturates now. The issue asked for every width to saturate at its own ends and
  the measurement said otherwise: a width under `int` takes the low bits of the saturated `int`, so `(byte)300.0`
  is 44 and `(byte)1e12` is 255. Proved by the conformance suite on both sides: 56 of the first 65 cases fail on
  main. Proposed and archived through OpenSpec (`transpiler-bcl`, `transpiler-expressions`).
