# Tasks

## 1. The format culture travels

- [x] 1.1 The server writes the request's format culture from .NET's data on every page (`CultureFormatBridge`, `CultureBridge`), boot installs it before hydration, and a culture switch fetches `/_equantic/culture/{name}.json`. Verified by the culture bridge's tests on the server and the runtime's culture tests
- [x] 1.2 A page with no culture installed formats in the invariant culture, and both sides of a conformance case run in the culture it names or the invariant one. Verified by `CultureHarnessTests`
- [x] 1.3 The string catalogs carry strings only, their format facts written by the server
- [x] 1.4 A calendar's first day and names are the format data's, one copy, its catalog gone from the shell and the switch's document. Verified by `CalendarNamesFixtureTests` and `calendar-names.spec.ts`, and the culture bridge's tests

## 2. A number in the culture

- [x] 2.1 A number with no specifier is the culture's text in every shape C# writes it (#454). Verified by `NumberTextInTheCultureConformanceTests`, ten cases in five cultures
- [x] 2.2 A number's kind travels to the formatter: a double's, a native integer's, and the culture's per mille, exponent and percent signs (#455). Verified by `NumberKindConformanceTests`
- [x] 2.3 `Convert.ToString`, a `StringBuilder`'s `Append` and `Insert` and a record's text write a number in the culture, and an integer never as `-0`. Verified by `NumberTextInTheCultureConformanceTests`
- [x] 2.4 `N`, `F`, `C` and `P` are laid out from the culture's `NumberFormatInfo` (#634). Verified by the format subset, byte for byte in seven cultures, ar-EG and fr-FR among them

## 3. A date in the culture

- [x] 3.1 `DateOnly`, `TimeOnly` and `DateTimeOffset` print through the formatter (#469). Verified by `DateTypeTextConformanceTests`
- [x] 3.2 A custom picture writes the culture's separators, the offset and the era (#470). Verified by `DatePictureConformanceTests`

## 4. The SDK's own text

- [x] 4.1 The runtime's transpiled components regenerate, their numbers written through the formatter, and the Mermaid layout writes its paths and view boxes in the invariant culture
- [x] 4.2 EQ2109 retired from docs/DIAGNOSTICS.md and the diagnostics baseline, and the BCL surface audit's verdicts for the date types' and the numbers' `ToString` moved to the formatter

## 5. The real thing

- [x] 5.1 The Compiler, Server, Web, runtime and Conformance suites green, the served runtime's budget regenerated with what its bytes bought
- [x] 5.2 The dashboard sample in a browser under pt-BR and es, the cultures it negotiates (es a neutral one, with the generic ¤): numbers and dates as the server rendered them, through hydration, a client navigation and a culture switch, with no console error

## 6. Documentation and archive

- [x] 6.1 The wiki's SupportedFeatures and Diagnostics pages in English and Portuguese, on the wiki branch named like this change's branch
- [x] 6.2 One docs/LEDGER.md line, and the change archived with `openspec archive`
