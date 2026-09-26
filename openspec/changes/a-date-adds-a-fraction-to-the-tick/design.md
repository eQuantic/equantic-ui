# Design

## How Dart does it

No row of `docs/FLUTTER-PARITY.md` applies. Dart's `DateTime` counts microseconds and adds only a
`Duration`, an integral count, so a fractional add is the caller's own rounding there. The rule to
follow is .NET's, and this change ports it rather than inventing one.

## Context

The twins are compat types that count .NET's 100-nanosecond ticks in a bigint, so a tick-precise
answer is within reach: only the conversion of a double count into ticks was wrong. It went
through `ticksFromUnit`, which scaled to milliseconds and rounded, the rule of .NET 6.

## Decisions

### Port `DateTime.AddUnits` as it is written

.NET 10 checks the count against the most whole units a date can move by, then splits it:
`(long)Math.Truncate(value) * ticksPerUnit + (long)(fraction * ticksPerUnit)`. The twin does the
same operations in the same order, in doubles where .NET uses doubles, so every rounding of the
product is the one .NET makes; only the integral part's product is taken in a bigint, where .NET's
long is exact too. Computing the fraction in exact arithmetic instead would be more accurate and
would disagree with .NET wherever the double product rounds.

### One conversion of a double into a long

.NET 9 and later convert a double to a long toward zero, NaN to zero, and saturated at the ends.
One helper does that, and every `Add*` reads through it: a `DateTime` never reaches the ends,
since its count is checked first, but a `TimeOnly` has no check and a product such as
`AddHours(1e20)` saturates before it wraps. NaN is why `AddDays(double.NaN)` adds nothing.

### The calendar is checked where .NET checks it

.NET's `Add*` reach the calendar through `AddTicks`, which refuses a result outside it, so the
twin's `addTicks` holds that check and every `Add*` of `DateTime` and `DateTimeOffset` reads
through it. `DateTimeOffset` then checks its UTC time, as its own `Add` does through
`ValidateDate`, and in that order: a clock time outside the calendar is refused in `DateTime`'s
words before the UTC time is looked at.

### `TimeOnly` keeps its own rule

`TimeOnly.AddHours` and `AddMinutes` are one product in .NET, not split, and wrap by the day with
no range check. Sharing `DateTime`'s split would move `AddHours(1.23456789)` by a tick.

## Risks / Trade-offs

- [A page that pushed a date past year 9999 now throws where it built an invalid date] → That is
  .NET's answer, and the migration line says so.
- [`DateTimeOffset`'s `+` and `-` operators reach `addTicks` too, and .NET names their parameter `t`
  where `AddTicks` names it `value`] → The throw is now where .NET throws, and only the parameter's
  name in the message differs for the operator.

## Not here

`DateTime`'s `Add(TimeSpan)`, `Subtract(TimeSpan)` and its `+` and `-` with a `TimeSpan` build the
date without a range check, and so do `AddMonths` and `AddYears`. They add no fraction and are not
this rule; each is its own task.
