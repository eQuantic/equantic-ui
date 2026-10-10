# Proposal

Closes #606 and #626, sub-issues of #565 (the transpiler's fences hold on every path).

## Why

The runtime's `dateTime` and `dateTimeOffset` factories took a constructor's components by how many
arguments they got, so every overload past the second read its arguments in the wrong places: a
`DateTimeKind` was read as the millisecond and bun exited, a `DateTimeOffset`'s millisecond was read
as its offset, and a microsecond was dropped (#606). A value built from out-of-range components was
built anyway, a `DateTime` carried no `Kind`, and an overload that takes a `Calendar` read the
calendar as a component.

The Falei.pt app reported three defects of the local time (#626): `DateTimeOffset.Now` carried offset
zero with the local clock, so it named the wrong instant anywhere east or west of UTC;
`LocalDateTime` answered the value's own clock; and `ToLocalTime()` did not exist, a TypeError at run
time.

## What Changes

- **eqc builds a date by its constructor's shape.** A new IR strategy, `DateTimeConstructionStrategy`,
  reads the bound constructor's parameters and calls the runtime's factory for that shape (`of`,
  `fromTicks`, `fromDateAndTime`, and `fromDateTime` for a `DateTimeOffset`), each argument in its
  parameter's place through `ParameterTemplate`, which now serves a creation as it serves a call: a
  named argument lands on its parameter and every argument still runs in the order it is written. An
  overload that takes a `Calendar` is a build error (EQ1004), and so is a creation the model cannot
  bind, where a count of its arguments was guessed at.
- **The runtime's factories are one per shape, and check what .NET checks.** A component, a kind or
  ticks out of range, and an offset that is not whole minutes, is past fourteen hours, or is not zero
  for a UTC `DateTime`, are refused in .NET's words and in .NET's order. The factory callable by
  argument count goes.
- **A `DateTime` carries its `Kind`.** `Now` and `Today` are local, `UtcNow` is UTC, `SpecifyKind`
  sets it, arithmetic keeps it, and equality, ordering and the hash leave it out, as .NET's do.
  `ToLocalTime()` and `ToUniversalTime()` convert by it, `Microsecond` and `Nanosecond` answer, and
  the JSON writes a UTC value with `Z` and a local one with its offset, as System.Text.Json writes
  them. Hydration reads them back as System.Text.Json does, `Z` a UTC time and an offset moved to the
  local time, where it dropped the zone, and `DateTime.Parse` moves a written zone to the local time,
  as .NET's Parse does. The formatter reads the kind too: `o` and `K` end a UTC time with `Z` and a
  local one with its offset, `z` writes the zone's offset, `U` moves only what is not UTC, and a letter
  alone that is no standard specifier is refused, where the kind was invisible to every text.
- **The local time is the browser's time zone.** `DateTimeOffset.Now` is the instant now at the
  browser's offset for it, `LocalDateTime` and `ToLocalTime()` read the zone at the value's instant,
  daylight saving included, and a `DateTimeOffset` of a `DateTime` that is not UTC takes the zone's
  offset for its clock time, a clock time a transition skips or repeats taking the standard one, as
  .NET's `TimeZoneInfo.GetUtcOffset` does. A local time made from an instant keeps which of two
  repeated hours it is, as .NET keeps it beside the kind, through arithmetic and `Date` until
  `SpecifyKind`, so its way back to UTC is that instant.
- **A date's text is read in linear time.** The ISO reader cut the trailing white space first and
  reads the space before a zone only when a zone follows, where a line of 50,000 spaces took 1.5 s.

For a developer using the SDK: every `new DateTime(...)` and `new DateTimeOffset(...)` C# accepts
builds the value .NET builds, `DateTimeOffset.Now` and `ToLocalTime()` show the user's local time,
and a `Calendar` overload fails the build instead of producing a wrong date. Nothing an app writes
changes. The public surface gains `DateTimeConstructionStrategy` in eqc's `PublicAPI.Unshipped.txt`;
the developer surface does not move.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `runtime-dates`: a constructor builds the value .NET builds, a `DateTime` carries its kind, and the
  local time is the browser's time zone.

## Impact

- eqc: `DateTimeConstructionStrategy` (new), `ParameterTemplate` (a creation's arguments),
  `DateTimeStrategy` and `DateTimeOffsetStrategy` (no longer build), the diagnostics baseline (EQ1004
  gains the strategy as a reporting site).
- The runtime: `utils/datetime.ts`, `utils/format.ts` (the kind in a date's text, one reading of the
  browser's offset), and the specs that called the factories by count.
- Tests: `DateTimeConstructionConformanceTests`, `LocalTimeConformanceTests` (the local ones in three
  time zones) and `DateTimeToStringConformanceTests`, both sides, `DateTimeConstructionTests` in the
  compiler suite, and `datetime.spec.ts` and `format.spec.ts`.
- The wiki's SupportedFeatures page, English and Portuguese.
