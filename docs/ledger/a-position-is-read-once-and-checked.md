- **2026-10-30 · A position in a sequence is read once and checked as .NET checks it**: bugs under #849
  ([#394](https://github.com/eQuantic/equantic-ui/issues/394),
  [#1160](https://github.com/eQuantic/equantic-ui/issues/1160)). `a[^n]` over an array, a list or a string was
  text that named its receiver twice, so `Get()[^1]` and every write through it called `Get` twice, and a
  deconstruction's target was refused; it is a place now, read and written through the subscript with the receiver
  evaluated once, the offset, then the length. The issue's other worry, an offset spliced bare, did not hold when
  measured: `a[^(i + 1)]` already answered right, since the parentheses its author wrote survive the text. A list's `Insert`, `RemoveAt`, `RemoveRange` and `GetRange` placed a
  named argument by its position in the call and clamped a position .NET refuses (`Insert(5, 9)` on a list of one
  appended); they take each argument in its parameter's place and check it in .NET's words through the runtime's
  `utils/list.ts`. Proved by the conformance suite on both sides, failing before. Proposed and archived through
  OpenSpec (`transpiler-expressions`, `transpiler-bcl`).
