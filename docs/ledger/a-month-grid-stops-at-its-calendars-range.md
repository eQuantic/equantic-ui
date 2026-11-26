- **2026-11-24 · A month grid stops at its calendar's range**: a Bug under #1587
  ([#1849](https://github.com/eQuantic/equantic-ui/issues/1849)). ar-SA's Um Al Qura calendar writes from April
  30th 1900 to November 16th 2077, and the grid's chevron past its last month, and its keyboard's page and
  section moves, asked a month the calendar refuses, so the press failed on both sides; at the Gregorian
  calendar's own edges, the years 1 and 9999, the grid failed to build. `CultureCalendar` gains `FirstDay` and
  `LastDay`, the grid stops there and counts its cells by day number, and the browser's calendars refuse a move
  past their range in .NET's words: Um Al Qura's and the Persian calendar's year range and the Persian
  calendar's last month, and a Gregorian or Thai date a `DateTime` cannot hold. Measured on both sides by
  `CultureCalendarTwinTests`; `CalendarTests` drives the grid at each edge. Proposed and archived through
  OpenSpec (`openspec/specs/month-grid`).
