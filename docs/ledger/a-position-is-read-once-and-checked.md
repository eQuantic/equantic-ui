- **2026-10-29 · A position in a sequence is read once and checked as .NET checks it**: bugs under #849
  ([#394](https://github.com/eQuantic/equantic-ui/issues/394),
  [#1160](https://github.com/eQuantic/equantic-ui/issues/1160)). `a[^n]` over an array, a list or a string was
  text that named its receiver twice and spliced its offset bare, so `Get()[^1] += 5` called `Get` three times and
  `a[^(i + 1)]` read past the end; it is a place now, read and written through the subscript with the receiver
  evaluated once, the offset, then the length. A list's `Insert`, `RemoveAt`, `RemoveRange` and `GetRange` placed a
  named argument by its position in the call and clamped a position .NET refuses (`Insert(5, 9)` on a list of one
  appended); they take each argument in its parameter's place and check it in .NET's words through the runtime's
  `utils/list.ts`. Proved by the conformance suite on both sides, failing before. Proposed and archived through
  OpenSpec (`transpiler-expressions`, `transpiler-bcl`).
