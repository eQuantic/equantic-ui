# Spec Delta

## ADDED Requirements

### Requirement: A builder has the members a page reaches

Every `StringBuilder` member a page reaches SHALL answer as .NET's does: `AppendFormat` and
`AppendJoin` append what `string.Format` and `string.Join` write, `Capacity` follows the chunks .NET
allocates through every edit, `MaxCapacity` and `EnsureCapacity` answer, the `Chars` indexer reads and
writes a char, `Length` cuts the text or fills it with `\0`, `Equals(StringBuilder)` compares the text
and `CopyTo` copies it, each refusing what .NET refuses. A member that cannot cross SHALL fail the
build.

#### Scenario: Capacity after appends and a Clear

- **WHEN** forty chars are appended one at a time to `new StringBuilder()` and it is cleared
- **THEN** its `Capacity` is 48, as in .NET, where `Capacity` read undefined

#### Scenario: AppendFormat

- **WHEN** `new StringBuilder("12").AppendFormat("{0}-{1}", 1, 2).ToString()` is read
- **THEN** it is "121-2", as in .NET, where `appendFormat` was a TypeError

#### Scenario: The indexer

- **WHEN** `b[0] = 'x'` runs on `new StringBuilder("12")` and `b[0]` is read
- **THEN** it is 'x', as in .NET, where the write set a property nobody read

#### Scenario: A culture an interpolation cannot format in

- **WHEN** `sb.Append(CultureInfo.InvariantCulture, $"x{n}")` is built
- **THEN** the build fails with EQ2108, where the browser was handed the provider as the value
