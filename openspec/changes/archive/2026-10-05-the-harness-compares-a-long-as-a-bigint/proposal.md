# Proposal

Closes #596, a sub-issue of #565 (the transpiler's fences hold on every path).

## Why

The conformance harness runs a C# block on .NET and its translation on bun, and compares the JSON
each side prints. The .NET side wrote a value as System.Text.Json writes it, and the JS side as
`JSON.stringify` writes the runtime's representation, so for a long, a decimal, an enum and a float
the comparison ran backwards: a right translation failed, and a wrong one passed. A long that
crossed as a BigInt printed `"5"` against .NET's `5`, and one that became a JS number printed `5`
and passed. The cases returned `.ToString()` instead, which hid the trap. Doubles past .NET's E
notation threshold, NaN, value tuples and pairs failed whatever the translation did, and
`return 5L;`, which imports nothing from the runtime, threw "JSON.stringify cannot serialize BigInt".

## What Changes

- **The harness writes a value as the runtime holds it.** The .NET side writes a long and a ulong as
  a BigInt's digits in a string, a decimal as its text, an enum by its member's twin name (a
  `[Flags]` enum and a value no member names by its number), a double and a float as JavaScript's
  `Number::toString` writes the double (NaN and the infinities as null, -0 as 0), and a value tuple
  and a `KeyValuePair` as arrays. The JS side prints through one helper whose replacer writes a
  BigInt as the runtime's own `toJSON` does, so a case prints the same whether or not it imported
  the runtime.
- **A `DateTimeOffset`'s unix times are longs, cut as .NET cuts them.** Re-run under that harness,
  one case of the 3,557 already in the suite failed: `ToUnixTimeSeconds()` answered a JS number for a
  long, which met the first long it was mixed with as a TypeError. `ToUnixTimeSeconds` and
  `ToUnixTimeMilliseconds` answer a BigInt now, and they cut the ticks to whole units from
  0001-01-01 before the epoch is taken off, as .NET does, where a division after the subtraction
  rounded an instant before 1970 toward the epoch.

For a developer using the SDK: `dto.ToUnixTimeSeconds() * 1000L` runs, and an instant before 1970
with a fraction of a second answers .NET's count. The harness is the project's own instrument and
moves nothing an app writes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `runtime-dates`: a `DateTimeOffset`'s unix times.

## Impact

- **Tests**: the conformance harness (`RuntimeJson`, `DotNetEvaluator`, `ConformanceRunner.Print`),
  `ReturnedValueConformanceTests`, `NumberTextTests`, `HarnessSelfChecksTests`.
- **Runtime**: `utils/datetime.ts`, `DateTimeOffset.toUnixTimeSeconds` and `toUnixTimeMilliseconds`,
  which answer a BigInt. The served runtime moves by a few bytes.
- **eqc**: none. The public surface and the developer surface do not move.
- Found on the way and filed: #606, the date constructors past the second.
