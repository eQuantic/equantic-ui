## 1. Convert

- [x] 1.1 Add `utils/number-convert.ts`: the conversions of an integer, a double, a decimal and an `object` to an integer, of an `object` to a double, a single, a bool and a char, and of an integer and a text to a char, with .NET's checks in its words.
- [x] 1.2 Lower `Convert.To<integer>`, `ToDouble`, `ToSingle`, `ToChar` and `ToBoolean` by the overload C# bound, and share the invalid cast's template.

## 2. A cast

- [x] 2.1 Add `saturate`, `saturateUnsigned` and `saturateLong` to `utils/overflow.ts`.
- [x] 2.2 Lower an unchecked cast of a double or a float through them in `ValueFlow`, a char's and an enum's included.

## 3. Tests

- [x] 3.1 A conformance test (`tests/eQuantic.UI.Conformance.Tests/NumberNarrowingConformanceTests.cs`): each row of the issues, each width at both ends, at a NaN and at the infinities, both sides executed.

## 4. Documentation

- [x] 4.1 The wiki's SupportedFeatures page, in every language it is written in.
- [x] 4.2 A ledger entry under `docs/ledger/`.
