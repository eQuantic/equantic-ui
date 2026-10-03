# Proposal

Closes #452, #480, #535 and #442, sub-issues of #164 (the transpiler's fences hold on every path).

## Why

An enum has no object of its own in the browser: a member is held as its camelCase name, and a
`[Flags]` member as its number. Every place that turns that back into what .NET answers did it on
its own, and several did not do it at all:

- A flags enum's `ToString()` indexed a name table with a number and answered null, a nullable enum's
  printed the camelCase name, and `"," + rank` indexed the cast's value table and wrote `undefined`
  (#452, #535).
- `Enum.Parse`, `TryParse`, `GetNames`, `GetValues` and `IsDefined` named an object `Status` that no
  module declares, and threw (#480).
- A dictionary keyed by an enum was refused by EqJson both ways, and a flags key was written by its
  names, which the browser does not hold (#442).

## What Changes

For a developer, an enum reads in the browser as it reads on the server, in every place the C# names:

- Its text: a member's name, a flags combination's set flags, a value no member names as its digits,
  a nullable one as nothing for null, and `ToString("D" | "X" | "F" | "G")` and an interpolation's
  format as .NET writes them, an alignment padding that text.
- The statics of `Enum`, through the runtime's enum functions (`$eq.enums`) with the enum's shape
  written at the call: `Parse` with its case and its errors, `TryParse` with the default (or, for the
  overload that takes a `Type`, null) on a failure, `GetNames` and `GetValues` in the order of the
  values, and `IsDefined` over the enum, a number, a name or an `object` holding any of them.
- A cast from an `object` holding the enum keeps it, a cast between two enums goes through both
  values, and a value no member names is held as its number.
- A dictionary keyed by an enum crosses both ways, a flags key as its number, and a name no member
  has is refused, as System.Text.Json refuses it.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-bcl`: an enum's text, its formats, the statics of `Enum`, its casts and its JSON.

## Impact

- **eqc**: `EnumShape` becomes the one owner of an enum's member table, read by the casts, the
  arithmetic, the ordering, the text and the statics. `EnumMethodStrategy`, `ToStringStrategy`,
  `InterpolatedStringStrategy`, `CastExpressionStrategy`, `BinaryExpressionStrategy`,
  `ValueOrdering`, `StringConversion` and `HydrationSpec` read it.
- **Runtime**: `utils/enums.ts` (`text`, `parse`, `tryParse`, `zero`, `names`, `values`,
  `isDefined`), reached as `$eq.enums`. `parseEnum` of `utils/format.ts` goes.
- **Server**: `EqJson` writes and reads an enum as a dictionary key, a flags enum as its number.
- **Public surface**: `Eq.ParseEnum` goes and `Eq.Enum*` names arrive (eqc's own constants, no app
  writes them). The developer surface does not move.
- **Break**: an enum value that names no member, sent to a Server Action, is refused with a JSON
  error where it was read as the enum's first member. Migration: send a member's name or its number.
