## ADDED Requirements

### Requirement: A move past the calendar's range is refused in its words

`CultureCalendar.AddMonths` and `AddYears` SHALL refuse a move to a month the culture's calendar does not
count with .NET's `ArgumentOutOfRangeException` in that calendar's words, in the browser as in .NET, and
`CultureCalendar.FirstDay` and `LastDay` SHALL answer the calendar's `MinSupportedDateTime` and
`MaxSupportedDateTime` as days.

#### Scenario: Past Um Al Qura's last year

- **WHEN** browser-side code in ar-SA evaluates `CultureCalendar.AddMonths(CultureCalendar.LastDay, 1)`
- **THEN** it throws "Valid values are between 1318 and 1500, inclusive. (Parameter 'year')", with the actual
  value 1501, as .NET does

#### Scenario: Past the Gregorian calendar's last day

- **WHEN** browser-side code in en-US evaluates `CultureCalendar.AddMonths(CultureCalendar.LastDay, 1)`
- **THEN** it throws "Year, Month, and Day parameters describe an un-representable DateTime.", as .NET does

### Requirement: The month grid stops at its calendar's range

`Calendar` SHALL NOT page past its calendar's first or last month, SHALL keep its keyboard's moves inside the
days the calendar writes, and SHALL draw a day the calendar does not write as a hole.

#### Scenario: The chevron past Um Al Qura's last month

- **WHEN** the next-month chevron is pressed on a `Calendar` counting in Um Al Qura and showing November 2077
- **THEN** the grid still shows that month, and nothing is refused

#### Scenario: December 9999

- **WHEN** a `Calendar` showing December 9999 is built in en-US
- **THEN** it draws the month's 31 days, its last week past them as holes, and its next-month chevron stays
