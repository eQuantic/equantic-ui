# Proposal

Closes #716 and #718, Bugs under #565 (The transpiler's fences hold on every path, continued), both
found by #715's null-argument measurement, which listed each overload as a gap.

## Why

Executed on both sides at 20069215, where 99 of the 102 cases of `ConvertEncodingConformanceTests` and
`DateTimeTryParseConformanceTests` fail:

- `Convert.ToBase64String`, `ToBase64CharArray`, `ToHexString` and `ToHexStringLower` were lowered to
  `String()` of their first argument. For `new byte[] { 1, 2, 255 }` .NET writes `AQL/` and `0102FF`,
  and the browser wrote `1,2,255`. The offset, the length and the options went nowhere, nothing was
  refused, and `ToBase64CharArray` wrote no character into its array (#716).
- `TryParse(string, out T)` of a `DateTime`, a `DateTimeOffset`, a `TimeOnly` and a `TimeSpan` called a
  `tryParse` the runtime did not have, a TypeError on the page, with the out argument handed over as a
  second argument. `DateOnly.TryParse` answered the date or null where .NET answers true or false, and
  wrote nothing through its out argument (#718).

## What Changes

- The runtime gains `$eq.convert.toBase64String`, `toBase64CharArray`, `toHexString` and
  `toHexStringLower`, .NET 10's `Convert` step for step: the same characters, a line break every 76
  characters under `InsertLineBreaks` and none after the last line, and the refusals in .NET's order
  with its messages. A `byte[]` is read whether C# built it (an array of numbers) or the platform did
  (`Encoding.UTF8.GetBytes`, a `Uint8Array`).
- eqc hands each overload to its twin by the overload C# bound, each argument in the twin's parameter
  it fills, named arguments evaluated in the order written. A span that is not a collection expression
  fails the build (EQ1004). With no model the argument count settles the overload, and what it cannot
  settle fails the build. A class of the app's named `Convert` keeps its own methods.
- The five time types gain a `tryParse` that answers the value their `Parse` reads, or undefined where
  Parse refuses the text or the text is null, and eqc lowers `TryParse(string, out T)` inline over it,
  as `Guid.TryParse` is: true with the value in the out argument, or false with the type's zero there.
  Only the refusals of `Parse` (a `FormatException`, an `OverflowException`, an `ArgumentException`)
  become that false; any other fault of the reader stays loud. `DateOnly.tryParse` answers undefined
  where it answered null. Every other overload of the five's
  `TryParse` (a provider, styles, a span) fails the build (EQ1004): handed to the twin it would read
  the text and never write its out argument, a wrong answer in silence where the page failed before.
- `TimeOnly.Parse` reads .NET 10's invariant grammar, as measured there: the whole text, a clock that
  exists, an AM or PM designator before or after the time in either case (`12 AM` is midnight, an hour
  past 12 is refused under AM and kept under PM), parts of one or two digits, an hour alone only beside
  a designator, and a fraction only after the seconds, rounded to the tick as .NET rounds it. Before,
  `9:30 PM` was half past nine in the morning, `25:00` a day and an hour, `09:30 junk` half past nine,
  and a fraction past its seventh digit was cut. `TimeOnly.Parse` and `DateOnly.Parse` refuse in
  .NET's words: `String '25:00' was not recognized as a valid TimeOnly.`
- The DatePicker reads a typed date with `DateOnly.TryParse`, which its comment said it wanted and
  could not say until now.

For a developer: `Convert.ToBase64String(bytes)` writes in the browser what it writes on the server,
and `if (DateTime.TryParse(text, out var when))` runs there instead of failing the page.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-bcl`: an array's encoding is .NET's, and a time type's `TryParse` answers as .NET's.

## Impact

- eqc: `ConvertStrategy`, the new `TimeTryParseStrategy`, `DefaultValue.TimeZero` (the zero of a time
  type by its name, which `DefaultValue.Of` now reads too) and four `Eq` constants (public surface:
  `PublicAPI.Unshipped.txt` adds the constants and the strategy).
- Runtime: the new `utils/convert-encoding.ts`, the `$eq.convert` namespace, and `tryParse` on the five
  time factories. The served runtime grows by the twins.
- The shared components: `DatePicker`'s pinned module changes where it parses.
- Not reached: the web realizer, Photon, the SDKs and the templates. The developer surface does not
  move.
- #715's null-argument baseline only shrinks, by 35: 17 overloads answer as .NET's now, and the 18
  probes of the `TryParse` overloads with a provider leave the measured surface, since the build
  refuses them.
- Left where they are: translating the `TryParse` overloads with a provider or styles (#717), and
  `FromBase64String` and `FromHexString`, which stay fenced (EQ2004). `TryParse` reads what `Parse`
  reads, so two of `Parse`'s gaps are `TryParse`'s too: with no provider it reads the invariant
  culture where .NET reads the current one (#806), and `DateTime`, `DateTimeOffset`, `DateOnly` and
  `TimeSpan` read fewer forms than .NET, `TimeSpan` some of them wrongly (#807).
