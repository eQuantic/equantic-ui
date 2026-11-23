# Proposal

Closes #1616, a Bug under #1587 (The transpiler's fences hold on every path, part five): an ISO date that
does not exist refused in other words than .NET's.

## Why

- `DateTime.Parse("2024-02-30T10:00")` throws in .NET "The DateTime represented by the string … is not
  supported in calendar 'System.Globalization.GregorianCalendar'.", in every culture, naming that
  culture's calendar. .NET's `ParseISO8601` reads the ISO form through to the end and checks the date and
  the clock together, so a day that does not exist is refused as a clock that does not exist is.
- The browser checked the date before it saw the `T`, and refused it as a text it could not read.

## What Changes

- Year-first numbers that name no date are kept as such until the reader knows whether a `T` follows:
  before one, the ISO clock is read, and a well-formed clock with a day that does not exist is refused as
  .NET refuses it, in the name of the culture's calendar; a clock malformed after the `T`, and a day that
  does not exist outside the ISO form, keep the plain refusal, as in .NET.

## Impact

- `src/eQuantic.UI.Runtime/src/utils/date-reading.ts`.
