## ADDED Requirements

### Requirement: A page reads its culture's date and time separators

Code that crosses to the browser SHALL read `DateTimeFormatInfo.DateSeparator` and `TimeSeparator`, through
`CultureInfo.CurrentCulture.DateTimeFormat` and through a `DateTimeFormatInfo` it holds, and SHALL get what
.NET answers for the page's culture.

#### Scenario: Three cultures

- **WHEN** a page reads `CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator` and `TimeSeparator` in en-US,
  de-DE and fi-FI
- **THEN** the browser answers `/` and `:`, `.` and `:`, and `.` and `.`, as .NET does
