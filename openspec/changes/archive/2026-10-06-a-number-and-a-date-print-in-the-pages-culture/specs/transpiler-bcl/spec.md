## ADDED Requirements

### Requirement: A number's text with no specifier is the culture's

A number written into text with no format specifier SHALL be its text in the culture in force, as
.NET writes it, wherever C# writes it: a concatenation, a plain or an aligned interpolation hole,
`ToString()`, `Convert.ToString`, `string.Format("{0}")`, `string.Concat`, `string.Join`,
`StringBuilder.Append`, `AppendLine` and `Insert` (the value written by position or by name, or an
interpolated string handed to one), and the text of a record, whose members are written as a
concatenation writes them. It SHALL carry the culture's decimal separator, minus sign and words for
NaN and the infinities, a float SHALL be written in its own digits, a decimal with its scale, and an
integer with no sign on a zero. The current culture or a null provider SHALL be the call with none, and
the invariant culture SHALL write the invariant text. An unsigned integer SHALL read the same in every
culture.

#### Scenario: A fraction in pt-BR

- **WHEN** `double n = -1.5;` is written as `$"{n}"`, `"v=" + n` and `n.ToString()` with pt-BR in force
- **THEN** each writes `-1,5`, as .NET does

#### Scenario: A negative integer in sv-SE

- **WHEN** `int i = -5;` is written as `$"{i}"` with sv-SE in force
- **THEN** it writes `−5`, with the culture's U+2212 minus sign, as .NET does

#### Scenario: An integer JavaScript holds as a negative zero

- **WHEN** `int half = neg / 2;` with `neg` holding `-1` is written as `$"{half}"`, `"v=" + half` and
  `half.ToString()`
- **THEN** each writes `0`, as .NET does, where the division truncates to `-0` in JavaScript

#### Scenario: A builder and a record

- **WHEN** `sb.Append(-1.5)` and the text of `record Point(double X, string? Name, bool On)` built with
  `(-1.5, null, true)` run with pt-BR in force
- **THEN** they write `-1,5` and `Point { X = -1,5, Name = , On = True }`, as .NET does

#### Scenario: A builder's value by name, and an interpolated string handed to it

- **WHEN** `sb.Append(value: -1.5)`, `sb.Insert(value: V(), index: I())` and `sb.Append($"{d}|{on}")`
  with `double d = -1.5; bool on = true;` run with pt-BR in force
- **THEN** they write `-1,5`, evaluate V before I and insert V's value at I's index, and write
  `-1,5|True`, as .NET does

#### Scenario: An aligned hole

- **WHEN** `double d = -1234.5;` is written as `$"[{d,10}]"` with de-DE in force
- **THEN** it writes `[   -1234,5]`, the culture's text padded, as .NET does

#### Scenario: A value written for a machine

- **WHEN** `d.ToString(CultureInfo.InvariantCulture) + "px"` runs with pt-BR in force
- **THEN** it writes `-1.5px`, as it does in every culture

### Requirement: A number's kind decides what its format writes

A number's format SHALL be decided by its C# type where the compiler knows it: `D`, `X` and `B` SHALL
refuse a `double` with .NET's FormatException whatever it holds, a `nint` and a `nuint` SHALL be
formatted as integers of the platform's width, and a value typed `object` SHALL be formatted by what it
holds. The per mille sign, the signs of an exponent and the percent sign SHALL be the culture's own.

#### Scenario: A whole double and an integer specifier

- **WHEN** `(2.0).ToString("D")` runs
- **THEN** it throws FormatException, as .NET does, where it printed `2`

#### Scenario: A native integer

- **WHEN** `nint m = -1;` is written with `m.ToString("X")`
- **THEN** it writes `FFFFFFFFFFFFFFFF`, as .NET does on a 64-bit host

#### Scenario: An integer held as an object

- **WHEN** `object o = 2;` is written with `string.Format("{0:D3}", o)`
- **THEN** it writes `002`, as .NET does
