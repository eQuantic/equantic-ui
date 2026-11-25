- **2026-11-24 · A date is read wherever .NET reads it**: three Bugs under #1587
  ([#1841](https://github.com/eQuantic/equantic-ui/issues/1841),
  [#1842](https://github.com/eQuantic/equantic-ui/issues/1842),
  [#1843](https://github.com/eQuantic/equantic-ui/issues/1843)), found reviewing #1616. The browser read the ISO
  form only where a text began, and now reads it after a designator, a clock or a zone, its clock in place of
  theirs, as .NET does (`PM 2024-01-15T10:00` is 10:00); a zone may lead the text. A day's name is read once
  anywhere between the parts and asked of the date last, after a clock that does not exist (`2024-01-15 10:00
  Monday` reads), where the browser read one only beside the date. A numeric date with two numbers of three
  digits or more, two years to .NET's lexer, is refused (`1/001/2024`). Measured on both sides by
  `DateOrderConformanceTests` in six cultures. Proposed and archived through OpenSpec (`openspec/specs/runtime-dates`).
