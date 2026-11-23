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
