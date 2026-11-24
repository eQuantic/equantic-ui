## ADDED Requirements

### Requirement: The ISO form is read after whatever came first

Reading a date SHALL read the ISO form, a year-first date, a `T` and its clock, after a designator, a clock,
a zone or a day's name, as .NET's lexer does: its clock takes the place of the one before it and of a
designator, a zone before it stands unless it writes its own, and a day's name beside it is not asked. A
zone SHALL be read before anything else too, and a second zone SHALL be the plain refusal.

#### Scenario: A designator before the ISO form

- **WHEN** browser-side code reads `PM 2024-01-15T10:00` with `DateTime.Parse`
- **THEN** it is 10:00 on January 15th 2024, as in .NET, where the browser named an unknown word at index 13

#### Scenario: A zone first

- **WHEN** browser-side code reads `Z 2024-01-15` and `Z 2024-01-15T10:00+01:00`
- **THEN** the first is that day in UTC and the second 09:00 UTC, as in .NET

### Requirement: A day's name stands once anywhere, and is asked of the date last

Reading a date SHALL read a day's name once, between any of its parts, and SHALL ask whether it is the date's
day only after the date and the clock are known to exist, of today where the text names no date. A second
day's name SHALL be the plain refusal.

#### Scenario: After the clock

- **WHEN** browser-side code reads `2024-01-15 10:00 Monday` and `2024-01-15 10:00 Tuesday`
- **THEN** the first is 10:00 on January 15th 2024 and the second is refused because the day of week was
  incorrect, as in .NET, where the browser named an unknown word at index 17 for both

#### Scenario: A word after a wrong day

- **WHEN** browser-side code reads `2024-01-15 Tuesday junk`
- **THEN** it names the unknown word at index 19, as .NET does before it asks the day

### Requirement: A date has one year

Reading a numeric date SHALL refuse one with two numbers of three digits or more, which .NET's lexer takes
each for a year.

#### Scenario: A day of three digits

- **WHEN** browser-side code reads `2024-01-001` and `1/001/2024`
- **THEN** each is refused as not recognized, as in .NET, where the browser read January 1st 2024
