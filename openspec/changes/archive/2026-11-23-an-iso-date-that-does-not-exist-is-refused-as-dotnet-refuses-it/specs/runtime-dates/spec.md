## ADDED Requirements

### Requirement: An ISO date that does not exist is refused in the calendar's words

Reading the ISO form, a year-first date, a `T` and its clock, SHALL refuse a day that does not exist as
.NET's `ParseISO8601` refuses it, once the clock is read, with the `FormatException` that names the
culture's calendar; a clock malformed after the `T`, and a day that does not exist outside the ISO form,
SHALL keep the plain refusal.

#### Scenario: The 30th of February

- **WHEN** browser-side code reads `2024-02-30T10:00` with `DateTime.Parse`
- **THEN** it throws "The DateTime represented by the string '2024-02-30T10:00' is not supported in
  calendar 'System.Globalization.GregorianCalendar'.", as in .NET, where the browser said the string was
  not recognized

#### Scenario: A malformed clock, and no T

- **WHEN** browser-side code reads `2024-02-30Tjunk` and `2024-02-30`
- **THEN** each throws "String '…' was not recognized as a valid DateTime.", as in .NET

#### Scenario: A T after a date that is not the ISO form's

- **WHEN** browser-side code reads `2024-02T10:00` and `1/15/2024T10:00`
- **THEN** each is refused plainly, as in .NET, where the browser read the first as the 1st of February
  2024, and `2024-01-15Tx` names an unknown word at its `T`

#### Scenario: An offset past fourteen hours is checked last

- **WHEN** browser-side code reads `2024-02-30T10:00+25:00`, and `2024-01-15T10:00+25:00 junk`
- **THEN** the first names the calendar and the second is refused plainly, as in .NET, which checks the
  offset's range after the rest of the text, the date and the clock; `2024-01-15T10:00+15` is still
  refused for its offset
