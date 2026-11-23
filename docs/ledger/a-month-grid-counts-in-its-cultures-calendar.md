- **2026-11-23 · A month grid counts in its culture's calendar**: a Bug under #1587
  ([#1617](https://github.com/eQuantic/equantic-ui/issues/1617)). `Calendar`, and the `DatePicker` that opens it,
  laid out a Gregorian month and titled it with the culture's own calendar's month names, so January 2024 was
  `دی 2024` in fa-IR, `محرم 2024` in ar-SA and `มกราคม 2024` in th-TH, whose calendar counts 2567. The vocabulary
  gains `CultureCalendar`, beside `CalendarNames`: a day's year, month and day in the format culture's calendar,
  its month's first day, and moves by months and years cut to the target month's last day, read from
  `DateTimeFormatInfo.Calendar` in .NET and from the calendar the culture's data carries in the browser (#1191).
  The grid counts its month, title, cells, announcements and moves through it, so in fa-IR January 17th 2024
  opens `دی 1402`, from December 22nd to January 20th. `CalendarNames`' `Intl` fallback names the Gregorian
  months, as a culture whose data did not travel counts them. Measured on both sides by
  `CultureCalendarTwinTests` in four cultures. Proposed and archived through OpenSpec (`openspec/specs/month-grid`).
