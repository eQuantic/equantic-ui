# Proposal

Closes #806 and #717, Bugs under #565 (The transpiler's fences hold on every path, continued), and
#844, found while measuring them, a Bug under #845 (the third part of the same feature, since #565
reached GitHub's limit of 100 sub-issues).

## Why

Executed on both sides at c25b177e, where 177 of the 198 cases of
`DateTextInTheCultureConformanceTests` fail (22 cases, each run in the invariant culture, en-US,
pt-BR, de-DE, sv-SE, ar, fi-FI, ja-JP and ko-KR):

- `Parse` and `TryParse` of a `DateTime`, a `DateTimeOffset`, a `DateOnly`, a `TimeOnly` and a
  `TimeSpan` with no provider read the invariant shapes whatever culture the page was in, where .NET
  reads the current culture. `DateTime.TryParse("01/02/2024", out d)` on a pt-BR page answered the
  2nd of January where .NET answers the 1st of February, with no failure anywhere; `15/01/2024` was
  refused; and a value written with `ToString()` did not read back in en-US or pt-BR (#806).
- The overloads with a provider crossed it as the name `CultureInfo`, which the runtime does not
  export, a ReferenceError on the page; `TryParse` with one failed the build (EQ1004) since #718.
  `ParseExact`, `TryParseExact` and `GetDateTimeFormats` did the same, with nothing at the build to
  say so (#717): 60 lines of the null-argument baseline carry it.
- `Convert.ToDateTime` answered the text of its argument for every overload, since the lowering of
  `System.Convert` wrote each `To…` it did not know as `String(value)`: a page that read the year of
  `Convert.ToDateTime("01/02/2024")` read `undefined` (#844).

## What Changes

- The runtime gains `utils/date-reading.ts`, a reader of the numeric forms a culture writes, as .NET
  10's `DateTimeParse` reads them: three numbers in the order of the culture's short date pattern, a
  year of three digits or more wherever it stands and one of two read against 2049; the separators
  `/`, `-` and `.` and the culture's own, with white space beside them; ar's marks of direction after
  a number; ko-KR's point after the day; a clock of one to three parts with `:` or the culture's own
  separator (fi-FI's point), a fraction after the seconds with a point or a comma in every culture,
  `AM` and `PM` and the culture's own designators before or after it; a zone (`Z`, `±h`, `±hh`,
  `±hmm`, `±hhmm`, `±hh:mm`), refused past fourteen hours. What it refuses is .NET's refusal in its
  words: a word it does not know at its index, a clock no day has, a zone out of range.
- The five twins' `parse` and `tryParse` read with it, in the page's culture, or in the invariant one
  when told so; a TimeSpan's fraction takes the culture's decimal separator where it has no days.
- eqc's `TimeParseStrategy` (the former `TimeTryParseStrategy`) claims `Parse` and `TryParse` of the
  five types, every overload, by the parameter each argument binds to: no provider, the current
  culture and a null read the page's culture, the invariant culture reads the invariant one, and any
  other culture fails the build (EQ2108). Styles that are white space are what Parse does with none,
  and any other, and a span, fail the build (EQ1004).
- `ParseExact`, `TryParseExact` and `GetDateTimeFormats` are no longer claimed by the types'
  strategies, so the fence refuses them at the build (EQ2004) instead of a ReferenceError on the page.
  Reading through a format is left for later.
- `Convert.ToDateTime` reads by the overload C# bound: a text as `DateTime.Parse` reads it, in the
  culture the call names, a null one as `MinValue`; an object by what it holds when the call runs; a
  date as itself; and a value of any other type throws .NET's InvalidCastException.

For a developer: `DateTime.Parse(text)` reads on a pt-BR page what it reads on a pt-BR server, a value
written with `ToString()` reads back, `DateTime.Parse(text, CultureInfo.InvariantCulture)` works in the
browser, and a call the browser cannot read is a build error rather than a page that fails.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `runtime-dates`: a date or a time is read in the culture the call names, and `Convert.ToDateTime`
  reads a text as `Parse` does.

## Impact

- eqc: `TimeTryParseStrategy` is renamed `TimeParseStrategy` and claims `Parse` as well; the public
  surface moves by that name (`PublicAPI.Unshipped.txt`). `ConvertStrategy` reads `ToDateTime`.
  `DateTimeStrategy`, `DateTimeOffsetStrategy` and `TimeSpanStrategy` leave the exact readers to the
  fence. EQ2108 gains a reporting site, documented in `docs/DIAGNOSTICS.md`.
- Runtime: the new `utils/date-reading.ts`, read by the twins in `utils/datetime.ts`, which gain an
  `invariant` flag on their readers and `dateTime.convert`.
- Not reached: the web realizer, Photon, the SDKs, the templates and the shared components, whose
  pinned modules do not change. The developer surface does not move.
- #715's null-argument baseline only shrinks, by 60 lines, and the BCL audit fences
  `GetDateTimeFormats`, which it counted as translated.
- Left for #807: a month's name, a date of two numbers, a clock with no date read as today's, the
  `GMT` word, a TimeSpan's `d:h:m:s` form, and calendars other than the Gregorian one.
