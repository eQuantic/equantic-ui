## ADDED Requirements

### Requirement: A date or a time is read in the culture the call names

`Parse` and `TryParse` of a `DateTime`, a `DateTimeOffset`, a `DateOnly`, a `TimeOnly` and a
`TimeSpan` SHALL read the numeric forms .NET 10 reads in the culture the call names: with no provider,
with `CultureInfo.CurrentCulture` and with a null one, the page's culture, which is the current one;
with `CultureInfo.InvariantCulture`, the invariant one. A date SHALL be three numbers in the order of
the culture's short date pattern, a year of three digits or more wherever it stands and one of two
read against 2049, separated by `/`, `-`, `.` or the culture's own separator; a clock SHALL be one to
three parts separated by `:` or the culture's own separator, with a fraction after the seconds written
with a point or a comma, and `AM`, `PM` or the culture's own designators before or after it; a zone
SHALL be `Z` or a signed offset of hours and minutes within fourteen hours. A text it does not read
SHALL throw .NET's FormatException in .NET's words, and `TryParse` SHALL answer false with the type's
zero. Any other provider SHALL fail the build (EQ2108), styles other than white space and a span SHALL
fail it (EQ1004), and `ParseExact`, `TryParseExact` and `GetDateTimeFormats` SHALL be refused by the
fence (EQ2004). The arguments SHALL be the ones bound to each parameter, by name or by place.

#### Scenario: A date in the page's culture

- **WHEN** `DateTime.TryParse("01/02/2024", out var d)` runs in pt-BR
- **THEN** it answers true and the 1st of February 2024, as in .NET, where it answered the 2nd of
  January

#### Scenario: What a culture writes reads back

- **WHEN** `DateTime.Parse(new DateTime(2024, 11, 5, 9, 5, 7).ToString())` runs in en-US, pt-BR,
  de-DE, sv-SE, ar, fi-FI, ja-JP and ko-KR
- **THEN** it answers the date it wrote in each, as in .NET

#### Scenario: The invariant culture on any page

- **WHEN** `DateTime.Parse("01/02/2024", CultureInfo.InvariantCulture)` runs in pt-BR
- **THEN** it answers the 2nd of January 2024, as in .NET, where the provider crossed as a name no
  browser defines

#### Scenario: A time span's fraction in the culture

- **WHEN** `TimeSpan.TryParse("1:02:03,5", out var t)` runs in pt-BR
- **THEN** it answers true and `01:02:03.5000000`, as in .NET, and `1.02:03:04,5`, which has days,
  is refused

#### Scenario: A refusal in .NET's words

- **WHEN** `DateTime.Parse("2024/01/15 21:30 junk")` runs
- **THEN** it throws a FormatException that names the unknown word at index 17, as in .NET

#### Scenario: Another culture

- **WHEN** `DateTime.Parse(text, CultureInfo.GetCultureInfo("pt-BR"))` is built
- **THEN** the build fails with EQ2108

### Requirement: Convert.ToDateTime reads a text as Parse does

`Convert.ToDateTime` SHALL answer by the overload C# bound: a text SHALL read as `DateTime.Parse` reads
it, in the culture the call names, and a null text SHALL be `DateTime.MinValue`; an object SHALL be
read by what it holds when the call runs, a null one being `MinValue`; a `DateTime` SHALL be itself;
and a value of any other type SHALL throw .NET's InvalidCastException after it is evaluated. A char
held in an object SHALL be read as a text of one character, since the browser holds a char as one and
a boxed value does not carry its type (#552).

#### Scenario: A text in the page's culture

- **WHEN** `Convert.ToDateTime("01/02/2024")` runs in pt-BR
- **THEN** it answers the 1st of February 2024, as in .NET, where it answered the text

#### Scenario: A null text

- **WHEN** `Convert.ToDateTime((string)null)` runs
- **THEN** it answers `DateTime.MinValue`, as in .NET, where it answered `"null"`

#### Scenario: A type with no conversion

- **WHEN** `Convert.ToDateTime(1)` runs
- **THEN** it throws an InvalidCastException, `Invalid cast from 'Int32' to 'DateTime'.`, as in .NET
