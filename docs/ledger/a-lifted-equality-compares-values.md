- **2026-10-17 · A struct's operator over nullable values is lifted as C# lifts it**: two nullable record
  structs compared with `==` or `!=` were JavaScript's `===` and `!==`, so two equal values built apart
  were unequal in the browser, which the spreadsheet's engine met comparing two `SheetRange?` and
  sidestepped; a `Color?` was compared the same way; and a struct's own operator over a nullable value
  was handed the null, so `a == b`, `a == null`, `a < b`, `a + b`, `-a` and `m += x` threw reading a
  member of it, and a conversion between two nullable types built a value holding null. The bound
  operator decides now: a lifted equality whose operator no twin carries takes `$eq.equals`, which is
  C#'s lift itself, and every call of a struct's own operator and conversion is lifted where the bound
  tree lifts it, an equality answering an absent value without the operator, a relation false and any
  other operator null, a tuple's nullable element included. An operator declared over the nullable
  values themselves is still called as written. Sixty-seven cases, and nine through an app's module
  graph with the spreadsheet's range among them, executed on both sides, pin it; 28 of the first and 3
  of the second failed before ([#948](https://github.com/eQuantic/equantic-ui/issues/948)). Proposed
  and archived through OpenSpec (`openspec/specs/transpiler-expressions`).
