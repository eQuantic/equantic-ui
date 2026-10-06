# Tasks

## 1. The format culture travels

- [x] 1.1 The server writes the request's format culture from .NET's data on every page (`CultureFormatBridge`, `CultureBridge`), boot installs it before hydration, and a culture switch fetches `/_equantic/culture/{name}.json`. Verified by the culture bridge's tests on the server and the runtime's culture tests
- [x] 1.2 A page with no culture installed formats in the invariant culture, and both sides of a conformance case run in the culture it names or the invariant one. Verified by `CultureHarnessTests`
- [x] 1.3 The string catalogs carry strings only, their format facts written by the server

## 2. A number in the culture

- [x] 2.1 A number with no specifier is the culture's text in every shape C# writes it (#454). Verified by `NumberTextInTheCultureConformanceTests`, ten cases in five cultures
- [x] 2.2 A number's kind travels to the formatter: a double's, a native integer's, and the culture's per mille, exponent and percent signs (#455). Verified by `NumberKindConformanceTests`

## 3. A date in the culture

- [x] 3.1 `DateOnly`, `TimeOnly` and `DateTimeOffset` print through the formatter (#469). Verified by `DateTypeTextConformanceTests`
- [x] 3.2 A custom picture writes the culture's separators, the offset and the era (#470). Verified by `DatePictureConformanceTests`

## 4. The SDK's own text

- [x] 4.1 The runtime's transpiled components regenerate, their numbers written through the formatter, and the Mermaid layout writes its paths and view boxes in the invariant culture
- [x] 4.2 EQ2109 retired from docs/DIAGNOSTICS.md and the diagnostics baseline, and the BCL surface audit's verdicts for the date types' and the numbers' `ToString` moved to the formatter

## 5. The real thing

- [ ] 5.1 The Compiler, Server, Web, runtime and Conformance suites green, the served runtime's budget regenerated with what its bytes bought
- [ ] 5.2 The dashboard sample in a browser under pt-BR and de-DE: numbers and dates as the server rendered them, through hydration and a client navigation, with no console error

## 6. Documentation and archive

- [ ] 6.1 The wiki's SupportedFeatures and Diagnostics pages in English and Portuguese, on the wiki branch named like this change's branch
- [ ] 6.2 One docs/LEDGER.md line, and the change archived with `openspec archive`
