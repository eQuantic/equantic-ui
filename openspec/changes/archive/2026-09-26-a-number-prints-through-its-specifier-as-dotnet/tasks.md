# Tasks

## 1. The formatter

- [x] 1.1 Write a number's exact decimal expansion: a double from its bits, a long from its BigInt, a decimal from its mantissa and scale
- [x] 1.2 Format E, F, N, P, C, G and a custom picture from those digits, a half rounded by the type
- [x] 1.3 Check: the runtime spec holds the expansion at a subnormal, the largest double, a negative zero and a carry off the front

## 2. The compiler

- [x] 2.1 Say an integer's kind to the formatter at ToString, an interpolation hole and a string.Format argument, where a specifier may be written
- [x] 2.2 Check: the conformance class executes fifteen cases on both sides, and all fifteen fail on main

## 3. Documentation

- [x] 3.1 Add the `docs/LEDGER.md` line citing #393
- [x] 3.2 Update the wiki's SupportedFeatures page, English and Portuguese, on a wiki branch named like this pull request's
- [x] 3.3 Archive this change before the merge

## 4. Review

- [x] 4.1 Write `X` and `B` at the integer's width, which the compiler passes as the kind (`int16`, `sbyte`…)
- [x] 4.2 Draw a custom picture as .NET draws it: sections, text, percent and per mille, exponents, scaling commas
- [x] 4.3 Take a precision past the 100 digits `Intl` writes after the point, and throw for a specifier the type does not take
- [x] 4.4 Group as .NET groups (es-ES), and write the culture's minus sign in `D`, `G` and `R` (sv-SE)
- [x] 4.5 Check: the conformance class grows to 34 cases, 16 of them failing without these fixes, and the cross-pinned fixture gains es-ES, sv-SE, the infinities, `E2` and two pictures
- [x] 4.6 File what the review found outside this change: #454, #455, #456
