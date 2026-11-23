- **2026-11-23 · An ISO date that does not exist is refused in .NET's words**: a Bug under #1587
  ([#1616](https://github.com/eQuantic/equantic-ui/issues/1616)). .NET reads the ISO form through to the end and checks
  the date and the clock together, so `2024-02-30T10:00` is refused as a clock that does not exist is, naming the
  culture's calendar; the browser checked the date before it saw the `T` and said the text was not recognized. The
  reader now carries year-first numbers that name no date to the ISO clock and refuses them there, and keeps the plain
  refusal for a malformed clock and for a day that does not exist outside the ISO form. A `T` before a clock is the
  ISO form's only after three numbers with the year first, so `2024-02T10:00`, which the browser read as a date, is
  refused as .NET refuses it, and a `T` a letter follows names an unknown word. An offset past fourteen hours
  is checked last, after the date and the clock, as .NET checks it. Measured on both sides by
  `IsoRefusalConformanceTests` in four cultures. Proposed and archived through OpenSpec (`openspec/specs/runtime-dates`).
