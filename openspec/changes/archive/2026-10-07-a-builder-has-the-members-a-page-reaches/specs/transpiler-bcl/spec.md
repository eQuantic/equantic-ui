# Spec Delta

## ADDED Requirements

### Requirement: A builder has the members a page reaches

Every `StringBuilder` member a page reaches SHALL answer as .NET's does: `AppendFormat` and
`AppendJoin` append what `string.Format` and `string.Join` write, `Capacity` follows the chunks .NET
allocates through every edit, `MaxCapacity` and `EnsureCapacity` answer, the `Chars` indexer reads and
writes a char, `Length` cuts the text or fills it with `\0`, `Equals(StringBuilder)` compares the text
and `CopyTo` copies it, each refusing what .NET refuses. A member that cannot cross SHALL fail the
build.

A member that .NET makes of several appends SHALL make them as .NET does, each growing the chunks and
refused on its own: an interpolated `Append` or `AppendLine` appends each part in turn, so a hole that
reads the builder sees the parts before it, and a hole prints as a plain interpolation's does;
`AppendLine` appends the line's end after the value; `Append(StringBuilder)` checks the maximum before
it copies; and a `Replace` past the maximum keeps the chunks it replaced first. A call that names its
arguments SHALL bind each to its parameter, evaluated in the order written, and a text longer than the
browser's string can hold SHALL be .NET's `OutOfMemoryException`, the builder unchanged.

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

#### Scenario: An interpolated append that reads the builder

- **WHEN** `b.Append($"{b.Length}{b.Length}-{b.Length}")` runs on `new StringBuilder()`
- **THEN** the builder holds "01-3", as in .NET, where every hole was read before the text was
  appended and it held "00-0"

#### Scenario: Named arguments out of their order

- **WHEN** `new StringBuilder("12").CopyTo(count: 2, destination: a, destinationIndex: 1, sourceIndex: 0)`
  runs on `a = { '-', '-', '-', '-' }`
- **THEN** `a` is "-12-", as in .NET, where the arguments reached the twin in the order written

#### Scenario: A text longer than the browser's string

- **WHEN** an append makes a text longer than the engine's longest string
- **THEN** it throws .NET's `OutOfMemoryException` and the builder is unchanged, where JavaScript's
  `RangeError` escaped every `catch (OutOfMemoryException)`
