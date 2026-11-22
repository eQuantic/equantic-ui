- **2026-11-22 · An int and a long wrap in the browser where .NET wraps them**: in C#'s default
  context, which is unchecked, the transpiler wrapped an `int` or a `long` only inside an explicit
  `unchecked`. `int.MaxValue + 1` on a variable answered 2147483648 in the browser, and a hash written
  without `unchecked` answered another number than on the server. This was a documented divergence,
  and a reader on r/csharp named it as the reason they kept integer-heavy C# on WASM. Every width now
  wraps where C# wraps it: an int sum is `| 0`, a product is `Math.imul`, and a long goes through
  `BigInt.asIntN`. A `checked` context still throws, and a constant the C# compiler folded is left as
  written. A postfix step in a void lambda is written as the assignment. Proved by 26 conformance cases
  on both sides (23 fail without the change; the other three are controls) and by the generated differential fold, which now
  overflows an int and a long on purpose (8 of 8 batches fail without it). The served runtime grows
  0.35% in gzip ([#1807](https://github.com/eQuantic/equantic-ui/issues/1807)). Proposed and archived
  through OpenSpec (`openspec/specs/transpiler-expressions`).
