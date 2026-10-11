# Proposal

Closes #454, #455, #469, #470, #471 and #634, sub-issues of #164 (the transpiler's fences hold on every
path) and of #565, its continuation.

## Why

The server renders a page in the request's culture, and the browser wrote numbers and dates its own
way, so the SSR markup and the hydrated page could print the same value two ways, and the browser
could print it two ways itself:

- A number written with no specifier, in a concatenation, a plain or an aligned interpolation hole or
  `ToString()`, was the browser's invariant text, where .NET writes the current culture's: `1.5` for
  `1,5` in pt-BR, a hyphen for sv-SE's U+2212 minus sign (#454). Only `string.Format` read the culture.
- The formatter guessed what the compiler knew: a whole `double` took `D`, `X` and `B` as an integer,
  where .NET throws, a `nint` and a `nuint` were formatted as doubles, and the per mille sign was `‰`
  in every culture (#455).
- `DateOnly`, `TimeOnly` and `DateTimeOffset` printed through their twins' own `toString`, which knew a
  few tokens, no specifier and no culture, and a provider crossed as a name no browser defines (#469).
- A custom date picture wrote `/` and `:` as they stand where .NET writes the culture's separators,
  and `z`, `zz`, `zzz`, `g` and `gg` as letters (#470).
- The page was handed its culture only when the app had a string catalog: with no `.resx`, the browser
  formatted in its HOST's locale, a date's patterns in the invariant culture, and the conformance
  suite passed or failed by the machine it ran on (#471).
- `N`, `F`, `C` and `P` were laid out by `Intl`, whose ICU is not .NET's: ar-EG's own digits where .NET
  writes ASCII ones, a no-break space where .NET's currency and percent patterns have a plain one, and
  two digits with no precision where .NET reads the culture's, three on ICU (#634). The format subset
  test folded the spaces on both sides, which hid it.

## What Changes

- The format culture travels with every page: `window.__EQ_CULTURE__.format` carries what the
  browser reads of it, its number symbols, separators, group sizes, digits and patterns, and its date
  patterns, separators, names and first day of the week, read from .NET's own `NumberFormatInfo` and
  `DateTimeFormatInfo` (`CultureFormatBridge`), whether or not the app has a string to translate. A
  calendar's names are this data too: they travelled as a second copy beside it. A culture switch with no reload fetches the culture it
  switches to from `/_equantic/culture/{name}.json`. With no culture installed, the browser formats
  in the invariant culture, never in its host's.
- A number written with no specifier, wherever C# writes it, is the culture's text, through the
  formatter that writes every other number: a concatenation, an interpolation hole, `ToString()`,
  `Convert.ToString`, `string.Format`, `string.Join`, a `StringBuilder` and a record's text. An integer
  writes no sign on a zero JavaScript holds as `-0`. An unsigned integer, a char and a non-negative
  integer constant read the same in every culture and are left as they are.
- `N`, `F`, `C` and `P` are laid out from the culture's `NumberFormatInfo` as .NET lays them out, and
  `Intl` only for a culture whose data did not travel. The ISO currency code the browser needed for
  `Intl` is no longer read, and no longer written.
- The compiler tells the formatter what a number is: a `double` says so, and `nint` and `nuint` are
  integers of the platform's width. The per mille sign, the signs of an exponent and the percent sign
  are the culture's own.
- `DateOnly`, `TimeOnly` and `DateTimeOffset` print through the formatter a `DateTime` prints through,
  each with what its type takes, its provider read as .NET reads it.
- A custom date picture writes the culture's date and time separators, the offset (`z`, `zz`, `zzz`)
  and the era (`g`, `gg`).
- The SDK's own components write a machine's text in the invariant culture: the Mermaid layout's
  curve paths and view boxes. Its coordinates are positive by construction today, and every culture
  writes a positive integer's digits alike, so nothing changes on a page, but a negative one would
  have been written with sv-SE's U+2212, which no path parser reads.
- EQ2109 (`ToString(CultureInfo.CurrentCulture)` with no specifier) is retired: the general format is
  the culture's on both sides now.
- No break for an app: its C# compiles to the new form. The public surface of `eQuantic.UI.Compiler`
  replaces `Eq.AsInteger` and `Eq.AsSingle` with `Eq.AsNumber` and gains `TypeSymbolExtensions.IsDate`,
  and `eQuantic.UI.Web` gains `CultureFormatBridge`. The developer surface does not move.

## Capabilities

### New Capabilities

- `page-culture`: the format culture a page is in, how it travels from the server and what the
  browser does with none installed.

### Modified Capabilities

- `transpiler-bcl`: a number's text with no specifier is the culture's, and a number's kind decides
  what its format writes.
- `transpiler-records`: a plain class or struct that writes no text of its own writes its type's full
  name, as .NET does (#570).
- `runtime-dates`: a `DateOnly`, a `TimeOnly` and a `DateTimeOffset` print as .NET prints them, and a
  custom picture writes the culture's separators, the offset and the era.

## Impact

- eqc: `StringConversion` and the interpolated string path write a number or a date through the
  formatter, `FormatKind` passes a number's kind, `ToStringStrategy`, `ConvertStrategy`,
  `StringBuilderStrategy`, `DateOnlyTimeOnlyStrategy` and `DateTimeOffsetStrategy` route their text to
  it, a record's text writes each member as a concatenation does, and the build's string catalogs carry
  strings only: the format facts they carried (the currency and the date patterns, from the build
  machine's ICU and for the UI culture) are the server's to write now.
- The runtime: `utils/format.ts` draws numbers and dates from the culture's data, `utils/culture.ts`
  installs and switches it, `CalendarNames` reads its names from it, and boot installs it from
  `__EQ_CULTURE__` before hydration.
- The server: `CultureBridge` writes the format culture on every page and serves a culture's format
  document, without the calendar catalog it wrote beside it, and `UIExtensions` maps it.
- Tests: conformance cases in five cultures, both sides run in the culture a case names and the
  invariant one when it names none, on every host; the format subset compared byte for byte, in seven
  cultures; the runtime's format tests; the culture bridge's.
- Docs: the wiki's SupportedFeatures and Diagnostics pages (English and Portuguese), docs/DIAGNOSTICS.md
  for EQ2109, and one docs/LEDGER.md line.
