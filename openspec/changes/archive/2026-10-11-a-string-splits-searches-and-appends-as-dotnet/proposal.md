# Proposal

Closes #753, #724 and #750, Bugs under #565 (The transpiler's fences hold on every path, continued).
#753 and #724 were found by #715's null-argument measurement, which listed each overload as a gap;
#750 by #748's second review.

## Why

Executed on both sides at 18509220, where 55 of the 73 cases of `StringSplitAndSearchConformanceTests`
fail:

- `string.Split` was lowered to JavaScript's `split` of its first argument. An array of separators was
  one text joined by commas (`" abcd ef ".Split(new[] { 'a', 'd' })` answered the whole string, .NET
  answers ` `, `bc` and ` ef `), a null one was the text "null", the count was dropped
  (`Split('d', 2)`), `TrimEntries` was ignored, and `RemoveEmptyEntries` was read from the options'
  spelling, so options held in a variable were lost. An empty separator split the text into its
  characters where .NET splits nothing, and so did an empty array, where .NET splits on white space
  (#753).
- `IndexOf(string, int[, int])` and `LastIndexOf(string, int[, int])` were lowered to JavaScript's
  searches, which take no count, read a start past the end as the end where .NET throws, and read
  `lastIndexOf`'s start as the place a match may BEGIN where .NET's is the place it must END by:
  `"abcabc".LastIndexOf("bc", 4)` answered 4, .NET answers 1, and `LastIndexOf("c", 5, 2)` did not stop
  at its count. A null value was searched as the text "null" (#724).
- `StringBuilder.Append` and `Insert` of a float, a double and an enum: measured again, each already
  writes .NET's text since #637, and nothing pinned it (#750). The rows #750 lists for an `object`
  holding a float or an enum still print what the browser holds; that is boxing losing the value's
  type, not the builder, and moves to #768 beside #552's identity half.

## What Changes

- The runtime gains `$eq.text.split` (one separator: a char or a string) and `$eq.text.splitAny` (a set:
  chars or strings), .NET 10's `SplitInternal` step for step: a negative count and an option .NET does
  not define are refused in .NET's order with .NET's messages; a null or empty single separator splits
  nothing; a null or empty set is .NET's white space; in a set, the first separator in the array's order
  that matches at a place wins; the count caps the entries, the last one taking the rest; `TrimEntries`
  trims .NET's white space and `RemoveEmptyEntries` drops the empty entries, the last one skipping past
  them. `$eq.text.splitOnWhiteSpace` goes: `Split()` is `splitAny` with no set.
- eqc hands each `Split` to its twin by the overload C# bound, the count and the options in their
  parameters' places as the values they are, named arguments evaluated in the order written, and chars
  a params parameter takes one by one as an array of them. A span handed whole fails the build.
- eqc hands `IndexOf` and `LastIndexOf` of a string from a start, and for a count, to the runtime's
  searches, which check the range and refuse a null value as .NET does. They search by ordinal, as the
  overload without a start already does here, where .NET searches by the current culture (#532).
- `StringBuilderConformanceTests` pins #750's rows on both sides.

For a developer: `text.Split(separators, count, options)` answers in the browser what it answers on the
server, whatever overload they write and wherever the options come from, and a search from a start or
for a count stops where .NET stops and refuses what .NET refuses.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-bcl`: a string splits as .NET splits, and a string's search from a start or for a count
  checks its range as .NET does with no comparison given as well as with one.

## Impact

- eqc: `StringMethodStrategy` (`Split`, and the start and count searches) and two `Eq` constants
  replacing one (public surface: `PublicAPI.Unshipped.txt` adds `StringSplit` and `StringSplitAny` and
  removes `SplitOnWhiteSpace`, an internal lowering target no app writes).
- Runtime: the new `utils/string-split.ts` and the `$eq.text` namespace. The served runtime grows by
  the two twins.
- Not reached: the web realizer, Photon, the SDKs and the templates. The developer surface does not
  move.
- #715's null-argument baseline only shrinks, and the BCL audit moves `Split`'s and the searches'
  verdicts to `eq`.
- Left for an issue of its own: a float or an enum boxed into `object` prints as the browser holds it
  (#768).
