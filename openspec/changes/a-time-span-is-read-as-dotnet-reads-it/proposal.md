# Proposal

Closes #857, a Bug under #845 (The transpiler's fences hold on every path, third part), split from
#807, whose date forms stay open.

## Why

Executed on both sides at 9b43eb2b, all 36 cases of `TimeSpanTextConformanceTests` fail (9 cases, each
run in the invariant culture, en-US, pt-BR and ar). `TimeSpan.Parse` and `TryParse` read one shape,
`[-][d.]hh:mm:ss[.f]`, and checked no part against its bound:

- `10` and `1:2` were refused where .NET reads ten days and an hour and two minutes, and so were
  `1:02:03:04` (a day, by the culture's pattern), `1:2:.5` and every form with parts of one digit.
- `24:00:00` was a day where .NET reads twenty-four, `00:60:00` an hour and `99999999.00:00:00` a span
  where .NET has none, and a fraction past seven digits was cut where .NET rounds it or refuses it.
- `Parse` threw only a FormatException, where .NET throws an OverflowException, in its own words, for
  a part past its bound; and `TimeSpan.MaxValue.ToString("g")` did not read back.

## What Changes

- The runtime gains `utils/timespan-reading.ts`, a port of .NET 10's `TimeSpanParse` for a text with
  no format: the text trimmed of .NET's white space, split into numbers of ASCII digits and the runs
  between them, read by how many numbers there are (days; hours and minutes; then the seconds, the
  days or a fraction; then two of them; then all five) and by the runs, matched against the invariant
  pattern `d.hh:mm:ss.fffffff` and the culture's `d:hh:mm:ss` with its decimal separator, each
  positive and then negative, in .NET's order. Each part is checked against its bound, a fraction past
  seven digits is rounded as .NET rounds it, and the whole is a long's arithmetic, so the most negative
  span reads.
- `TimeSpan.Parse` reads through it and throws what .NET throws: a FormatException for a text it does
  not read, an OverflowException for a part or a whole past its bounds, each in .NET's words, and the
  IndexOutOfRangeException .NET itself throws for a fraction whose leading zeros and digits come to
  fifteen or more. `TryParse` answers false for the first two, as .NET does.
- Nothing changes in eqc: the provider was already read as the culture it names (#850).

## Impact

- `src/eQuantic.UI.Runtime/src/utils/timespan-reading.ts` (new) and `datetime.ts`'s `timeSpan.parse`.
- The served runtime grows by the reader.
