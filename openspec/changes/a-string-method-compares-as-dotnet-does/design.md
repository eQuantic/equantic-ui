# Design

## Context

See proposal.md for why. #390 gave the statics (`string.Compare`, `string.Equals` with a comparison)
their rules in the runtime's `utils/string-statics.ts`: the comparison crosses as its member's
camelCase name and is checked at run time, a culture comparison goes to `Intl.Collator`, and
`ordinalIgnoreCase` ports .NET 10's `OrdinalCasing.CompareStringIgnoreCase`. The instance methods
never reached them: `StringMethodStrategy` read the comparison from the last argument's text and
lower-cased both sides, and `CompareToStrategy` compared code units.

## How Flutter answers it

Dart's `String` takes no comparison: `contains`, `indexOf` and `startsWith` compare code units, and a
search that ignores case lower-cases both sides, which is the defect this change removes. The rule is
.NET's, because the source is C#. docs/FLUTTER-PARITY.md has no row for string comparisons.

## Goals / Non-Goals

**Goals:** a string's comparing overloads answer as .NET does or are refused; the overload is read
from the bound method; the runtime keeps one rule per operation, shared with the statics.

**Non-Goals:** the overloads without a comparison, which search by the current culture in .NET
(#532); the collator's answers for widths and kana types under IgnoreCase, which `Equals` now shares
with the statics (#533); the char overloads with a start and a count (#534).

## Decisions

- **The bound method chooses, by its parameters' types.** The strategy reads the overload from
  `IMethodSymbol.Parameters` as a shape (string, char, int, bool, `StringComparison`, `CultureInfo`),
  never from an argument's spelling, which is what dropped a comparison held in a variable. A named
  argument written out of order is bound to its parameter by the helper the static strategies use,
  and the template writer keeps the order the arguments run in. With no model to ask (the playground
  compiles one buffer alone), the count of the arguments and a comparison spelled last are the only
  evidence, and they are read as such. `CultureInfo` is matched by its name and namespace: the
  parameter is `CultureInfo?`, whose display name carries the annotation.
- **Port .NET's ordinal casing rather than lower-case.** `toLowerCase` and `toUpperCase` are full
  Unicode case maps; .NET's ordinal comparison that ignores case takes each code point's simple upper
  case, with its own exceptions, and reads a surrogate pair as its code point. The statics already
  hold that map (`ordinalUpper`). The searches reuse it and port `Ordinal.IndexOfOrdinalIgnoreCase`
  (a value whose first unit is ASCII compared window by window with `CompareStringIgnoreCase`) and
  `OrdinalCasing.IndexOf` and `LastIndexOf` (any other value compared unit by unit, a pair by its
  code point and a lone half by itself), which is why half of a pair is found inside one.
- **Refuse a culture search rather than approximate it.** .NET searches by a culture with ICU's
  `usearch` behind an ASCII fast path, and JavaScript has no search: a search built from
  `Intl.Collator` on grapheme boundaries answers -1 for `"a\r\nb".IndexOf("\n", InvariantCulture)`,
  where .NET answers 2. A wrong answer in the browser is worse than a refusal at the build, so a
  constant culture comparison in a search is refused (EQ1004), and one in a variable throws after
  every check .NET makes before it searches. `Equals` compares whole strings, which the collator
  does, so it shares the statics' rule; `CompareTo(string)` is `string.Compare(a, b)`.
- **The arguments in the order C# writes them.** The runtime's `indexOf` and `lastIndexOf` take what
  follows the value as each overload lists it (a comparison; a start and a comparison; a start, a
  count and a comparison), told apart by their number. With the comparison first, every call with a
  range reordered its arguments, and the writer bound all of them in an arrow function to keep C#'s
  order, in hot paths such as the code editor's tokenizers.

## Risks / Trade-offs

- [An ordinal search pays a runtime call where it was a native method] → the ordinal path checks its
  arguments and calls JavaScript's own `startsWith`, `endsWith`, `indexOf` or `split` and `join`.
- [A build that passed fails with EQ1004 for a culture search] → named in the proposal as the break,
  with its migration line; the SDK and the samples already search ordinally.
- [`Equals` by a culture comparison that ignores case now equates a fullwidth letter with its ASCII
  one, and katakana with hiragana, as the static does] → measured and filed (#533). Lower-casing kept
  those apart by accident and missed the soft hyphen and a decomposed accent, which the collator
  answers as .NET does.
