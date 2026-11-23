# Proposal

Closes #1766 and #1767, two Bugs under #1587 (The transpiler's fences hold on every path, part five): a
decimal that lost its digits on the way to an integer, and `Math` over a decimal that answered NaN.

## Why

- An explicit cast of a decimal to an integer went through the decimal's double before it was cut
  (#1766). A long past 2^53 lost its last digits (`(long)9007199254740993m` was 9007199254740992), and
  a decimal just under a whole number was rounded up before the truncation: `(int)0.99999999999999999m`
  was 1 where .NET's is 0, and `(int)2147483647.9999999999999999m` threw where .NET's is int.MaxValue.
  `Currency.ToMinor` converts this way.
- `Math.Abs`, `Max`, `Min`, `Sign` and `Clamp` over a decimal fell through to JavaScript's `Math`,
  which reads the runtime's Decimal as NaN, with no build error (#1767). `decimal.Abs` and its siblings
  were refused by the build.

## What Changes

- A decimal cast to `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `char`, `nint`
  or `nuint`, and `decimal.ToInt32` and its siblings, is cut toward zero from the decimal's own digits,
  then checked against the type's range, in .NET's words for each type, inside `unchecked` too.
- The numeric table's decimal row answers every member `Math` has for a decimal: `Abs`, `Sign`, `Max`,
  `Min` and `Clamp` beside `Truncate`, `Floor` and `Ceiling`, with the scale .NET keeps (`Max` keeps the
  first of two equal values, `Min` the second). `Clamp` refuses a minimum above its maximum with .NET's
  `ArgumentException`, the values written in the current culture. `decimal`'s own statics of the same
  names reach the same row.
- A `Math` member over a decimal the table has no row for is refused by the build, never sent to
  JavaScript's `Math`.

## Impact

- `src/eQuantic.UI.Compiler`: `PrimitiveStaticStrategy` (the decimal row), `ValueFlow` (a decimal cut
  into an integer), `MathStrategy` (the refusal), and two `Eq` constants.
- `src/eQuantic.UI.Runtime`: `Decimal.abs`, `sign`, `max` and `min`, `truncatedFromDecimal` in
  `number-convert.ts`, and `utils/decimal-math.ts` for the clamp.
- The transpiled `Currency` converts its minor units through `truncatedFromDecimal`.
