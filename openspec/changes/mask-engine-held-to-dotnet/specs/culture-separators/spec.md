## Purpose

What a page's code reads of its culture's date and time formatting in the browser, as .NET answers it for the page's culture: the date and time separators a mask or a format writes.

## ADDED Requirements

### Requirement: A page reads its culture's date and time separators

Code that crosses to the browser SHALL read `DateTimeFormatInfo.DateSeparator` and `TimeSeparator`, through
`CultureInfo.CurrentCulture.DateTimeFormat` and through a `DateTimeFormatInfo` it holds, and SHALL get what
.NET answers for the page's culture.

#### Scenario: Three cultures

- **WHEN** a page reads `CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator` and `TimeSeparator` in en-US,
  de-DE and fi-FI
- **THEN** the browser answers `/` and `:`, `.` and `:`, and `.` and `.`, as .NET does
