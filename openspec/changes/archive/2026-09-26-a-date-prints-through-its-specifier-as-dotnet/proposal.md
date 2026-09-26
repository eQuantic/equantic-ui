# Proposal

Closes #388, a sub-issue of #164.

## Why

A `DateTime`'s own `ToString` reached the twin's `toString(pattern)`, which knew custom tokens
only, so a standard specifier was read as a pattern: `d.ToString("D")` printed `D` and
`d.ToString("d")` printed `24`. A provider crossed to the browser as a name no browser defines:
`d.ToString("G", CultureInfo.InvariantCulture)` threw `CultureInfo is not defined`. Measured on the
way, the formatter a date takes in `string.Format` wrote the round-trip and sortable
forms (`o`, `s`) from `toISOString()`, which is UTC, so a page off UTC shifted the hour and `o`
spelled a `Z` a wall-clock value does not have. It had no `R`, `u` or `U`, and a custom picture
replaced six tokens by text, so `d/M/yyyy` printed `d/M/2026` and `dd MMM yyyy` printed
`24 09M 2026`.

## What Changes

- **A `DateTime`'s `ToString` goes through the formatter**, as a number's does: a standard
  specifier from the culture's patterns, a custom picture token by token, and the provider through
  `NamedCulture` (the invariant culture invariant, the current culture or a null as the call with
  none, any other EQ2108). A null `DateTime?` writes nothing.
- **Every one-letter standard specifier** prints as .NET prints it: `o`, `s`, `u` and `R` from the
  value's own parts (a wall-clock value, .NET's `Unspecified`), `R` in the invariant culture's
  names, and `U` from the value read as local time and moved to UTC.
- **A custom picture is drawn as a culture's own patterns are**, with the fraction of a second
  (`f`, `F`, exact from a DateTime's ticks), `K`, `%` and quoted text.
- **With no culture in force, a date takes the invariant patterns**, as a number's text does,
  where `Intl`'s en-US presets printed `9/24/26`. And a year below 100 stays itself, where `Date`
  read it as 1900 plus it.

## Impact

- The compiler: `ToStringStrategy`, `DateTimeStrategy`.
- The runtime: `utils/format.ts` (`formatDate`, `renderPattern`, `asJsDate`).
- No public signature moves.
