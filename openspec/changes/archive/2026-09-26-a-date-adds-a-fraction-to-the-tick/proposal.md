# Proposal

Closes #422, a Bug under #164 (The transpiler's fences hold on every path).

## Why

The runtime's twins of `DateTime`, `DateTimeOffset` and `TimeOnly` add a fractional count the way
.NET 6 did, scaled through milliseconds and rounded, where .NET 7 and later land on the tick:
`AddSeconds(0.00001)` moves nothing on the web and 100 ticks in .NET 10, and `AddMilliseconds(0.5)`
moves twice what .NET moves. Around it, three `Add*` the BCL audit grades `native` call methods the
twins do not have, and a count or a result out of range builds an invalid date where .NET throws.

## What Changes

- **A fractional `Add*` lands on .NET 10's tick.** `DateTime` and `DateTimeOffset` turn the whole
  units and the fraction into ticks apart and truncate the fraction toward zero, as
  `DateTime.AddUnits` does, for days, hours, minutes, seconds, milliseconds and microseconds.
  `TimeOnly.AddHours` and `AddMinutes` take one product, converted as .NET 9 and later convert a
  double (toward zero, NaN to 0, saturated at the ends) and wrapped by the day. NaN adds nothing,
  as it does in .NET 10.
- **The missing `Add*` exist**: `DateTime.AddMicroseconds`, `DateTimeOffset.AddMilliseconds` and
  `DateTimeOffset.AddMicroseconds`, each a TypeError in the browser until now.
- **An out-of-range count or result throws in .NET's words**: "Value to add was out of range." for
  a count past what a date can move by, "The added or subtracted value results in an
  un-representable DateTime." for a result outside the calendar, and, for a `DateTimeOffset` whose
  UTC time leaves it, "The UTC time represented when the offset is applied must be between year 0
  and 10,000." `AddTicks` checks the same range, since .NET's `Add*` reach the calendar through it.

For a developer using the SDK: `date.AddSeconds(0.5)` and its siblings give on the web the value
they give in .NET, and a date pushed past year 9999 throws the exception .NET throws. Nothing is
written differently.

The part reached is the runtime (`src/eQuantic.UI.Runtime/src/utils/datetime.ts`), and the served
runtime's budget with it. eqc does not change. Neither the public surface nor the developer surface
moves. The migration line: a fractional `Add*` lands on the tick where it rounded to the
millisecond, and an out-of-range one throws where it built an invalid date.

## Capabilities

### New Capabilities

- `runtime-dates`: what the runtime's twins of the date and time types answer for .NET's date
  arithmetic.

### Modified Capabilities

None.

## Impact

`src/eQuantic.UI.Runtime/src/utils/datetime.ts` and its specs, conformance cases run on both sides,
the served runtime's budget, one `docs/LEDGER.md` line, and the wiki's SupportedFeatures page in
English and Portuguese.
