# Proposal

Closes #528, a Bug under #164 (The transpiler's fences hold on every path).

## Why

A string's own methods that take a `StringComparison` read the comparison from its SPELLING and
lower-cased both sides, and `CompareTo` ordered by code unit. Measured through the conformance
harness, 28 of the 48 cases this change adds answered differently in the browser on main: the Kelvin
sign matched a k under `OrdinalIgnoreCase`, a comparison held in a variable was dropped, `Replace`
dropped its own and read `$&` in its replacement as a pattern, a start past the end clamped where
.NET throws, and a sort written with `CompareTo` put every capital first.

## What Changes

- **An overload that compares reaches the runtime, chosen by the bound method.** `Equals`,
  `StartsWith`, `EndsWith`, `Contains`, `IndexOf` and `LastIndexOf` given a `StringComparison` (with
  a start and a count, and the char overloads), `Replace(string, string, StringComparison)` and
  `Replace(string, string)` call `$eq.text`. The comparison crosses as the value it is, so one held
  in a variable is the one it holds, and one .NET does not define throws .NET's words.
- **An ordinal search that ignores case is .NET's**, ported from .NET 10's `Ordinal` and
  `OrdinalCasing`: the Kelvin sign is not a k, the long s is not an s, the dotless i is not an I, a
  Greek letter with an iota subscript matches its title case, a surrogate pair matches by its code
  point, and half of a pair can be found inside one.
- **A start and a count are checked as .NET checks them**, with .NET's words, where JavaScript
  clamped; `LastIndexOf` steps back from one past the end as .NET's `CompareInfo` does.
- **`Replace` writes its replacement as text** (it was `replaceAll`, which reads `$&`), a null
  replacement removes the matches, and a null or empty old value throws.
- **`CompareTo(string)` is the current culture's comparison**, as `string.Compare(a, b)` is, so a sort
  answers `a,A,b,B`. `CompareTo(object)` is refused with EQ1004: a char is a string in the browser,
  where .NET throws for one.
- **A search by a culture comparison is refused.** The browser has no culture-aware search: `Intl`
  compares whole strings and searches nothing. A constant culture comparison in `StartsWith`,
  `EndsWith`, `Contains`, `IndexOf`, `LastIndexOf` or `Replace` fails the build with EQ1004, and one
  that arrives in a variable throws at run time, naming the method, the comparison and the fix. An
  overload that takes a `CultureInfo` (`StartsWith(string, bool, CultureInfo)`, `EndsWith` alike,
  `Replace(string, string, bool, CultureInfo)`) is refused the same way. `Equals` by a culture
  comparison compares whole strings, through the statics' collator (#390), and stays.
- The runtime's searches take their arguments in the order C# writes them, so a call is written as
  C# wrote it, with no arrow function binding its arguments to keep the order they run in.

For a developer using the SDK: the C# they wrote answers in the browser as it does on the server.
**BREAKING** for an app that searched by a culture comparison: the build fails with EQ1004 where
the browser answered the ordinal result. Migration: search by `StringComparison.Ordinal` or
`StringComparison.OrdinalIgnoreCase`, or compare whole strings with `Equals` or `string.Compare` by
the culture.

The parts reached are eqc (`StringMethodStrategy`, `CompareToStrategy`, eight `Eq` constants, and
`IsNamed`, which reads a type's name without a reference type's nullable annotation) and
the runtime (`utils/string-search.ts`, reached through `$eq.text`). The write-once components'
ordinal searches (the code editor's languages, the patch, Markdown and Mermaid parsers, the TSV codec)
call the runtime's. The public surface grows by the eight `Eq` constants; the developer surface does
not move.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-bcl`: a string's own comparing methods and `CompareTo` answer as .NET does, and a
  search by a culture comparison is refused.
- `transpiler-expressions`: a null-conditional call whose translation is a helper awaits an
  argument that awaits in the method it is written in, where the arrow that bound its receiver made
  the module unparsable; a receiver that is not a local refuses such a tail until #539.

## Impact

`src/eQuantic.UI.Compiler` (two strategies and `Eq`), the runtime's `utils/string-search.ts` and
`utils/string-statics.ts`, the runtime's transpiled components (pins regenerated), the BCL audit
baseline (fifteen overloads move from native to the runtime, and one is fenced), and tests in the
conformance, compiler and runtime suites. Filed beside it under #164: #532 (the overloads without a
comparison search by the current culture in .NET), #533 (a culture comparison that ignores case
equates widths and kana types .NET keeps apart) and #534 (the char overloads with a start clamp, and
drop their count).
