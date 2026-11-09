## Why

Closes #551 and #555, two Bugs under #164 (the transpiler's fences hold on every path).

A `long`- or `ulong`-backed enum was held in the browser as a JavaScript number, where the rest of the
SDK holds a 64-bit integer as a BigInt (`utils/long.ts`), so a member past 2^53 was a number that is not
its value: `(long)Huge.Top` of `[Flags] enum Huge : long { Top = 1L << 62 }` answered
4611686018427388000, its `D` and `X` formats lost the low digits, `Enum.GetValues` ordered by the
rounded values, and the server wrote its value as a JSON number the browser rounded. An enum's
operators were JavaScript's own, which compute in signed 32 bits: `Huge.A | Huge.B` with `B = 1L << 40`
answered 1, a `uint` flag's high bit read negative, so `(U.A | U.Top).HasFlag(U.Top)` was false, and
`~` of a byte flag was negative. Measured on main, 32 of the 47 conformance cases this change adds first
fail there.

## What Changes

- **A 64-bit enum is held as a BigInt, as a `long` is**: its flags members' literals, its default, its
  constants, a value no member names, the shape the enum functions read, and its JSON. A non-flags enum
  is still held as its member's camelCase key.
- **A cast is C#'s enumeration conversion**: the value the enum holds, converted as its underlying type
  converts. `(int)aWideFlag` takes the low 32 bits, `(long)` of a narrower enum is a BigInt, `(double)`
  of a non-flags enum reads its value where it took its key, and a checked context throws where .NET
  throws. `foreach (Status s in numbers)` converts each number, where every element became the zero
  member.
- **An enum's operators compute on its values at the underlying type's width**: `|`, `&`, `^`, `~`, `+`,
  `-`, `++`, `--`, their compound assignments and lifted forms, the relations and a relational
  pattern, `HasFlag` (on any enum, its flag read once), and `CompareTo` (a flags enum's, which called a
  method no number has).
- **The runtime** reads a 64-bit enum's shape as BigInts, unboxes and finds only the underlying type,
  in .NET's words (`(Huge)(object)1` is an InvalidCastException), and orders a BigInt exactly.
- **The server** writes a 64-bit flags enum's value and key as its digits, reads a `ulong` enum's number
  past `long`'s range, and writes a page's top-level enum field through EqJson, where a flags field
  crossed by its members' names. Hydration reads a 64-bit flags enum, as a value and as a key, as a
  BigInt.
- Fixed in passing, in the same code: `GetValueOrDefault()` of a nullable enum wrote the member's
  declared name, and an enum property's default crashed eqc for a `ulong` member past `long`'s range.

For a developer using the SDK: nothing they write changes. A 64-bit enum, and every enum's operators,
answer in the browser what they answer in .NET.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-bcl`: a new requirement, "An enum is held and combined at its underlying type's width".

## Impact

- **eqc**: `EnumShape` (the width, a value's literal and the held literal), the new `EnumConversion`
  and `EnumOperators`, `EnumStrategy`, `CastExpressionStrategy`, `ValueFlow`, the binary, unary and
  assignment strategies, `EnumHasFlagStrategy`, `CompareToStrategy`, `PatternConverter`,
  `ConstantLiteral`, `DefaultValue`, `NullableStrategy`, `GetHashCodeStrategy`, `HydrationSpec`,
  `TsStandIn`, `LambdaExpressionStrategy` and `TypeScriptEmitter`.
- **The runtime**: `utils/enums.ts`, `utils/ordering.ts`.
- **The server**: `Json/EqJson.cs`, `Rendering/ServerRenderingService.cs`.
- **Public surface**: none; every new member is internal. The developer surface does not move.
- **The wire**: a 64-bit flags enum crosses as a JSON string of its digits, as a `long` does, where it
  was a JSON number; the server still reads a number. Nothing an app writes meets it.
