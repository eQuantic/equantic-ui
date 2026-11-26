## MODIFIED Requirements

### Requirement: A .NET property or field nothing carries fails the build

A read of a property or a field of a .NET type that no translation answers SHALL fail the build with
EQ2004, naming the member, unless a value the browser holds carries the member by its own name: a
string's, an array's and a span's `Length`, the members an exception's twin carries, a decimal's
`Scale`, a `KeyValuePair`'s `Key` and `Value`, and the scalar members of a `NumberFormatInfo`. A member
an interface declares SHALL NOT be refused, since the value read through the interface may be a type of
the app's own. A chain of such reads SHALL be refused once, at its first member, whether it is read
through `.` or through `?.`, and a read made on a value a translation answered SHALL be asked as any
other. A culture handed to a call that reads it from how the call names it SHALL NOT be read as a value.

#### Scenario: A culture's property

- **WHEN** a component reads `CultureInfo.CurrentCulture.TextInfo.ListSeparator`
- **THEN** the build fails with one EQ2004 naming `System.Globalization.CultureInfo.CurrentCulture`;
  in the browser `CultureInfo` was not defined

#### Scenario: A chain through `?.`

- **WHEN** a component reads `error?.TargetSite?.Name`, where `error` is an `Exception`
- **THEN** the build fails with one EQ2004 naming `System.Exception.TargetSite`

#### Scenario: A member the runtime does not carry

- **WHEN** a component reads `Environment.NewLine`, `typeof(string).Name` or `new List<int>().Capacity`
- **THEN** the build fails with EQ2004 naming the member; each read undefined in the browser

#### Scenario: A member the browser carries

- **WHEN** a component reads `"ab".Length`, an exception's `ParamName`, a dictionary pair's `Key` or
  an `IGrouping`'s `Key`, or calls `char.ToUpper('i', CultureInfo.InvariantCulture)`
- **THEN** the build reports nothing

#### Scenario: A decimal's scale

- **WHEN** browser-side code reads `(1m / 3m).Scale` and `100.50m.Scale`
- **THEN** they are 28 and 2, as in .NET

## ADDED Requirements

### Requirement: A culture's number format is read as .NET reads it

`NumberFormatInfo.CurrentInfo` and `CultureInfo.CurrentCulture.NumberFormat` SHALL read in the browser
as the page's culture's number format, and `NumberFormatInfo.InvariantInfo` and
`CultureInfo.InvariantCulture.NumberFormat` as the invariant culture's, with every scalar member
(separators, symbols, digit counts and patterns) answering what .NET answers for that culture. A write
to a member and a read of `NumberGroupSizes`, `CurrencyGroupSizes` or `PercentGroupSizes` SHALL fail the
build with EQ2004.

#### Scenario: Where the currency's symbol goes

- **WHEN** browser-side code reads `NumberFormatInfo.CurrentInfo.CurrencyPositivePattern` and
  `CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator`
- **THEN** they are `3` and `,` in pt-PT, `0` and `.` in en-US, `2` and `,` in nl-NL, as in .NET

#### Scenario: The invariant culture on any page

- **WHEN** browser-side code on a pt-PT page reads `NumberFormatInfo.InvariantInfo.CurrencySymbol`
- **THEN** it is `¤`

#### Scenario: A write

- **WHEN** a component assigns `NumberFormatInfo.CurrentInfo.CurrencySymbol`
- **THEN** the build fails with EQ2004

### Requirement: A decimal truncates, floors and ceils as .NET does

`decimal.Truncate`, `decimal.Floor` and `decimal.Ceiling`, and `Math`'s over a decimal, SHALL answer the
whole number .NET answers, in its direction and with the scale .NET keeps, on the runtime's `Decimal`.

#### Scenario: A negative fraction

- **WHEN** browser-side code computes `Math.Truncate(-0.5m)`, `Math.Floor(-0.5m)` and `Math.Ceiling(-0.5m)`
- **THEN** they are `0`, `-1` and `0`, as in .NET

#### Scenario: Whether an amount has more digits than its currency

- **WHEN** browser-side code compares `decimal.Truncate(12505m / 10m)` with `12505m / 10m`
- **THEN** they differ, as in .NET
