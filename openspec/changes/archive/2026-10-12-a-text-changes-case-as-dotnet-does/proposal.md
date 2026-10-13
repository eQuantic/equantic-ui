# Proposal

Closes #848, a Bug under #845 (The transpiler's fences hold on every path, third part), and #725, a
Bug under #565 (the same feature, continued), whose three lines are the whole of what the
null-argument baseline carried for a dropped culture or calendar.

## Why

Executed on both sides at 9b43eb2b, 72 of the 96 cases of `CasingConformanceTests` fail (12 cases,
each run in the invariant culture, en-US, pt-BR, de-DE, tr-TR, az-Latn-AZ, lt-LT and el-GR):

- `ToUpper`, `ToLower` and their invariant pair, of a string and of a char, were the engine's
  `toUpperCase` and `toLowerCase`. Those apply Unicode's FULL mappings, which lengthen a text: `ß` is
  `SS`, `ﬁ` is `FI`, `ᾳ` is `ΑΙ`, and `İ` lower-cased is `i` with a combining dot. They read a final
  sigma in its context, so `ΟΔΟΣ` lowered to `οδος` where .NET writes `οδοσ`. .NET changes each code
  unit, or surrogate pair, to exactly one other, by the SIMPLE mappings of its own Unicode data, so a
  text's length never changes and an index read before the call still holds after it (#848).
- The engine knows no culture, and eqc dropped the one a call named. On a tr-TR page `"i".ToUpper()`
  answered `I` where .NET answers `İ`, the invariant culture upper-cased `ı` to `I`, which .NET keeps,
  and `char.ToUpper(c, null)` answered where .NET throws (#725).
- `new DateOnly(year, month, day, calendar)` dropped its calendar, a null one included, which .NET
  refuses; the `DateTime` and `DateTimeOffset` constructors with a calendar already failed the build
  (#725's null-argument baseline carried it).
- Engines of a later Unicode than the ICU .NET 10 cases by on a Linux or Windows server (bun 1.4,
  Node 22, the browsers of their year) case letters .NET does not there, such as `ƛ`, whose capital came
  with Unicode 16; the ordinal comparison that ignores case read the same engine and shared that
  difference. .NET cases by the host's ICU, and Apple's on macOS 26 is Unicode 16's: the runtime
  follows the servers, where a page is rendered and hydrated.

## What Changes

- The runtime gains `utils/casing.ts`, `$eq.text.toUpper` and `$eq.text.toLower`: a text with none of
  the characters an engine cases otherwise is cased by the engine whole, and any other one code point
  at a time, each taking .NET's simple mapping: a mapping that lengthens is the code point itself, but
  for the Greek letters with an iota subscript, which take the capital with a prosgegrammeni; a capital
  sigma is the medial one; a code point a server's .NET 10 does not case keeps its case; and the two i's are the
  culture's, tr and az by their language, the invariant culture keeping `İ` and `ı`. A char's culture
  handed as null throws .NET's ArgumentNullException, after the char is evaluated.
- eqc lowers `ToUpper` and `ToLower` and their invariant pair to the twins, of a string and of a char:
  no culture, `CultureInfo.CurrentCulture` and a null are the page's culture, which is the current
  one; `CultureInfo.InvariantCulture` and the invariant pair are the invariant one; any other culture
  fails the build (EQ2108), as it does for reading a date. A char's casing with a culture taken as a
  delegate fails the build (EQ1004), since the culture would be a value the browser cannot hold.
- `NamedCulture.ReadsInvariant` is the one reader of a culture argument for the two policies that
  take the page's culture and the invariant one, reading a date and changing case.
- `new DateOnly(…, calendar)` fails the build (EQ1004), as the `DateTime` and `DateTimeOffset`
  constructors with a calendar do.
- The ordinal comparison that ignores case reads the invariant culture's simple upper case from the
  same table, but for the long s, which it keeps, so the table of the Greek letters with an iota
  subscript is written once and the letters a server's .NET 10 does not case match only themselves on
  any engine.

For a developer: `"straße".ToUpper()` is `STRAßE` in the browser as on the server, `"i".ToUpper()` on a
tr-TR page is `İ`, `ToUpperInvariant()` is the same on every page, and a culture the browser does not
know is a build error rather than a casing that quietly ignored it.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-bcl`: a text and a char change case as .NET does, in the culture the call names.
- `runtime-dates`: a date built with a calendar fails the build.

## Impact

- eqc: `StringMethodStrategy` and `CharMethodStrategy` lower the casing to the twins; `NamedCulture`
  gains `ReadsInvariant`, which `TimeParseStrategy` and `ConvertStrategy` read too, so EQ2108's
  reporting site for a date moves there (`docs/DIAGNOSTICS.md`); `DateOnlyTimeOnlyStrategy` refuses a
  calendar through `DateTimeConstructionStrategy.RefusesCalendar`. The public surface gains
  `Eq.ToUpper` and `Eq.ToLower` (`PublicAPI.Unshipped.txt`).
- Runtime: the new `utils/casing.ts`, exported under `$eq.text`; `utils/string-statics.ts` reads its
  invariant upper case for `ordinalUpper`.
- The shared components embedded in the runtime: the seven whose C# cases a text or a char with
  `ToLowerInvariant` or `ToUpperInvariant` (`CodeCompletion`, `CodeEditorController`, `CodeFuzzyMatch`,
  `CodeKeymap`, `CodeLanguages`, `DataTable`, `MarkdownParser`) call the invariant twin where their
  modules called the engine's casing, and nothing else in those modules changes.
- Not reached: the web realizer, Photon, the SDKs and the templates. The developer surface does not
  move.
- #715's null-argument baseline only shrinks, by 3 lines. The BCL audit moves the eight casing
  members from native to the runtime.
