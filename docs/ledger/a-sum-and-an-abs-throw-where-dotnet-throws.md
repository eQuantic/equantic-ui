- **2026-11-22 · A sum, an absolute value and a copied sign throw where .NET throws**: `Enumerable.Sum` over an
  `int` or a `long` adds in a checked context whatever the context of the call, and `Abs` and `CopySign` of a
  signed integer's `MinValue` throw, since its magnitude is no value of its type. The browser kept counting: a
  sum answered 2147483648 for an int and a BigInt past 64 bits for a long, and an absolute value answered the
  magnitude. An int's and a long's sum now add through the runtime's checked addition (`$eq.num.addInt`,
  `$eq.num.addLong`), in the type the bound overload adds, and a signed integer's `Abs` and `CopySign` go through
  the runtime, which throws with .NET's message. The cases also found a `long?` and a `decimal?` sum with a null
  in it throwing a TypeError, now passed over, and `int.CopySign(0, -1)` answering `-0`. A real's sum stays an
  inline reduce: a runtime loop measured five times slower on a million doubles, and the runtime's `checked`
  two and a half times on ints and seven on longs, where the additions of their own cost nothing measurable
  ([#1808](https://github.com/eQuantic/equantic-ui/issues/1808)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-sequences`, `openspec/specs/transpiler-bcl`).
