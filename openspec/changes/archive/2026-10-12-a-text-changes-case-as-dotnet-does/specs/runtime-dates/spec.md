## ADDED Requirements

### Requirement: A date built with a calendar fails the build

A constructor of a `DateTime`, a `DateTimeOffset` or a `DateOnly` that takes a `Calendar` SHALL fail
the build (EQ1004), a null calendar included, since the browser has none of .NET's calendars.

#### Scenario: A DateOnly with a calendar

- **WHEN** `new DateOnly(2024, 1, 2, new GregorianCalendar())` is built
- **THEN** the build fails with EQ1004, where the calendar was dropped

#### Scenario: A DateOnly with a null calendar

- **WHEN** `new DateOnly(2024, 1, 2, null)` is built
- **THEN** the build fails with EQ1004, where .NET throws and the browser answered the date
