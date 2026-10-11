## ADDED Requirements

### Requirement: An array's encoding is .NET's

`Convert.ToBase64String`, `Convert.ToBase64CharArray`, `Convert.ToHexString` and
`Convert.ToHexStringLower` SHALL answer what the overload C# binds answers in .NET 10, each argument
filling the parameter it binds to, named or not, evaluated in the order written, for a `byte[]` C#
built and for one the platform built (`Encoding.UTF8.GetBytes`) alike. Base64 SHALL be padded with `=`
and, under `Base64FormattingOptions.InsertLineBreaks`, SHALL break its lines every 76 characters with
`\r\n` and none after the last line. `ToBase64CharArray` SHALL write the characters into the array it
is handed from its offset, leave the rest of it as it was, and answer their count. A null array, a
negative length or offset, a range past an array's end and an option .NET does not define SHALL throw
.NET's exception with its message, in the order .NET checks them. A span that is not a collection
expression SHALL fail the build.

#### Scenario: An array's base64 and hex

- **WHEN** `Convert.ToBase64String(new byte[] { 1, 2, 255 })` and
  `Convert.ToHexString(new byte[] { 1, 2, 255, 16 }, 1, 2)` run
- **THEN** they answer `AQL/` and `02FF`, as in .NET, where both were `String()` of the array

#### Scenario: Line breaks

- **WHEN** fifty-eight bytes are written with `Base64FormattingOptions.InsertLineBreaks`
- **THEN** the text is one line of 76 characters, `\r\n` and four more, as in .NET, and fifty-seven
  bytes are one line with no break

#### Scenario: A range past the end

- **WHEN** `Convert.ToBase64String(new byte[3], 2, 2)` runs
- **THEN** it throws .NET's `ArgumentOutOfRangeException` with the message `offset ('2') must be less
  than or equal to '1'. (Parameter 'offset')`, the actual value on the next line

### Requirement: A time type's TryParse answers as .NET's

`TryParse(string, out T)` of a `DateTime`, a `DateTimeOffset`, a `DateOnly`, a `TimeOnly` and a
`TimeSpan` SHALL answer true and write the value its `Parse` reads through the out argument, or answer
false and write the type's default there for a text its `Parse` refuses and for a null one, and SHALL
never throw. The out argument SHALL be any place C# can write, a discard, a variable declared before
and an array's element included, and the arguments SHALL be evaluated in the order written.
`TimeOnly.Parse` SHALL read the whole text and refuse an hour past 23, a minute past 59 and a second
past 59, as .NET refuses them. Every other overload of the five's `TryParse` (a provider, styles, a span)
SHALL fail the build.

#### Scenario: A text that is a date

- **WHEN** `DateTime.TryParse("2024-01-15", out var d)` runs
- **THEN** it answers true with that date in `d`, as in .NET, where the page failed with a TypeError

#### Scenario: A text that is no date

- **WHEN** `DateOnly.TryParse("abc", out var d)` runs
- **THEN** it answers false with `DateOnly.MinValue` in `d`, as in .NET, where it answered null

#### Scenario: A clock that does not exist

- **WHEN** `TimeOnly.TryParse("25:00", out var t)` runs
- **THEN** it answers false, as in .NET, where `Parse` read a day and an hour

#### Scenario: An overload with a provider

- **WHEN** a page calls `DateTime.TryParse(text, null, out var when)`
- **THEN** the build fails (EQ1004), where the page failed when the call ran
