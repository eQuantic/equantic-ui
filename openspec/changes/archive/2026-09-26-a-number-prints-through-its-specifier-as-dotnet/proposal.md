# Proposal

Closes #393, a Bug under the feature #164 (The transpiler's fences hold on every path).

## Why

The resx format subset admits the `E` specifier (EQ2100), and the runtime's formatter had no branch
for it, so `{0:E2}` passed the build and printed `12345` where .NET prints `1.23E+004`. Measuring the
rest of the formatter found the same class of gap in every specifier: .NET writes a formatted double
from its EXACT binary value (`(0.1).ToString("F20")` is `0.10000000000000000555`), and `toFixed` and
`Intl` start from the shortest text that reads back; .NET rounds a half by the type (a double's and a
float's to even, a decimal's and an integer's away from zero), and the formatter had one rule; and a
long or a decimal lost its digits past a double's. A new conformance class fails all fifteen of its
cases on main.

## What Changes

- The runtime formats a number from its exact decimal expansion (`utils/exact-decimal.ts`): a double's
  binary value written in full with BigInt, a long's own digits, a decimal's mantissa over its scale.
  `E`/`e` is written as .NET writes it (the mantissa's digits, the sign, an exponent of at least three
  digits); `F`, `N`, `P`, `C`, `G` and a custom picture round the exact digits by the type's rule.
- The compiler says which number it passes where the type decides the rounding: a float already said
  so (#378), and an int, a short, a byte and their unsigned twins now do too, to `ToString(format)`,
  to an interpolation hole with a specifier, and to a `string.Format` argument where the template may
  write one (`FormatKind`, `$eq.text.asInteger`).

Nothing changes for a developer using the SDK but the text a page prints, which is now .NET's: no C#,
setting or package moves. The public surface gains one `Eq` constant, `AsInteger`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-bcl`: a number prints through a format specifier as .NET prints it.

## Impact

`src/eQuantic.UI.Runtime` (`utils/format.ts`, `utils/exact-decimal.ts`, `eq.ts`), `src/eQuantic.UI.Compiler`
(`FormatKind`, `ToStringStrategy`, `InterpolatedStringStrategy`, `StringStaticStrategy`, `Eq`), one
conformance class and one runtime spec, `docs/LEDGER.md`, and the wiki's SupportedFeatures page.
