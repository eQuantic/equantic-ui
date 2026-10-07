# Proposal

Closes #534, a Bug under #164 (The transpiler's fences hold on every path).

## Why

`IndexOf(char, int)`, `IndexOf(char, int, int)`, `LastIndexOf(char, int)` and
`LastIndexOf(char, int, int)` were JavaScript's `indexOf` and `lastIndexOf`. JavaScript takes no
count, so the count was dropped, and it clamps a start outside the string where .NET throws:
`"abcabc".IndexOf('c', 0, 2)` answered 2 where .NET answers -1, and `"abc".IndexOf('a', 4)` answered
-1 where .NET throws. Measured through the conformance harness, 9 of the 16 cases this change adds
answer differently in the browser on main.

## What Changes

- **A char's search with a start reaches the runtime.** The four overloads call `$eq.text.indexOfChar`
  and `$eq.text.lastIndexOfChar`, ported from .NET 10's `String.Searching.cs`: an ordinal search, as
  every char search is, over the range the start and the count give.
- **The range is checked as .NET checks it, in .NET's words.** For `IndexOf` the start may stand at
  the end of the string. For `LastIndexOf` it must stand on a char of it, so one past the end
  throws (the string overloads step back from there instead), and an empty string answers -1 for any
  start and count. The start is checked before the count.
- `IndexOf(char)` and `LastIndexOf(char)` with no start stay JavaScript's, which answers them alike.

What does not move: the developer's C#, and nothing on the developer surface. The public surface gains
two constants, `Eq.StringIndexOfChar` and `Eq.StringLastIndexOfChar`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-bcl`: a new requirement for a char's search with a start and a count.

## Impact

- eqc: `StringMethodStrategy`, which shares the comparing overloads' runtime call, and `Eq`.
- The runtime: `utils/string-search.ts` and the `$eq.text` namespace.
- The wiki's SupportedFeatures page, in English and Portuguese.
