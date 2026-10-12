## ADDED Requirements

### Requirement: A TimeSpan writes the format it is given

A `TimeSpan` written with a format, through `ToString(format)`, `ToString(format, provider)`, an
interpolation hole or a composite placeholder, SHALL write what .NET 10 writes: `c`, `t` and `T` the
constant form `[-][d.]hh:mm:ss[.fffffff]` in every culture, `g` the short general form
`[-][d:]h:mm:ss[.FFFFFFF]` and `G` the long one `[-]d:hh:mm:ss.fffffff`, both with the culture's
decimal separator, and a custom format token by token without a sign. A null or an empty format SHALL
be the constant form. Any other format SHALL throw .NET's FormatException with its message. The
invariant culture SHALL write the invariant separator, the current culture and a null provider SHALL
be the call with none, and any other provider SHALL fail the build (EQ2108). A null `TimeSpan?` SHALL
write nothing.

#### Scenario: A custom format

- **WHEN** `new TimeSpan(0, 1, 2, 3, 4).ToString(@"hh\:mm")` runs
- **THEN** it answers `01:02`, as in .NET, where it wrote `01:02:03.0040000`

#### Scenario: A general format in a culture

- **WHEN** `$"{new TimeSpan(1, 2, 3, 4, 500):g}"` runs in pt-BR
- **THEN** it answers `1:2:03:04,5`, as in .NET, and `ToString("G", CultureInfo.InvariantCulture)` of
  the same value answers `1:02:03:04.5000000`

#### Scenario: A format .NET refuses

- **WHEN** `TimeSpan.Zero.ToString("h")` and `TimeSpan.Zero.ToString("'abc")` run
- **THEN** each throws a FormatException with .NET's message, where both wrote `00:00:00`

#### Scenario: A null TimeSpan

- **WHEN** a null `TimeSpan?` is written with `ToString()` and in a hole with `g`
- **THEN** each writes nothing, as in .NET, where `ToString()` read it through null
