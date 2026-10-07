# Tasks

## 1. A date is built by its constructor's parameters

- [x] 1.1 Measure what .NET builds and refuses for each constructor of `DateTime` and `DateTimeOffset`, the messages included
- [x] 1.2 The runtime: one factory per constructor shape (`of`, `fromTicks`, `fromDateAndTime`, `fromDateTime`), checking what .NET checks in .NET's order; the factory callable by count goes
- [x] 1.3 eqc: `DateTimeConstructionStrategy` over the bound constructor, `ParameterTemplate` for a creation, a `Calendar` overload refused (EQ1004)
- [x] 1.4 Prove it on both sides: `DateTimeConstructionConformanceTests` (the shapes, the refusals and the order of named arguments), failing on the base and passing with the fix, and `DateTimeConstructionTests` for the emitted shapes

## 2. A DateTime carries its kind, and the local time is the browser's

- [x] 2.1 `Kind` on the runtime's `DateTime`: set by the constructors, `Now`, `UtcNow` and `SpecifyKind`, kept by arithmetic, left out of equality, ordering and the hash; `ToLocalTime`, `ToUniversalTime`, `Microsecond`, `Nanosecond`, and the JSON's `Z` and offset
- [x] 2.2 `DateTimeOffset.Now`, `LocalDateTime` and `ToLocalTime` read the browser's zone; a `DateTimeOffset` of a `DateTime` takes the zone's offset as `TimeZoneInfo.GetUtcOffset` does
- [x] 2.3 Prove it on both sides in three time zones (`LocalTimeConformanceTests`: Asia/Kolkata, America/Sao_Paulo, Europe/Lisbon), the zone set through `TZ` for .NET and the engine alike
- [x] 2.4 The JSON reads a zone back as System.Text.Json does, in hydration, and `DateTime.Parse` moves a written zone to local time, proved by the cases that return a value of a kind and parse a zone

## 3. Documentation and the suites

- [x] 3.1 The wiki's SupportedFeatures page, English and Portuguese, on the wiki branch of this pull request
- [x] 3.2 `docs/LEDGER.md`: one line citing #606 and #626
- [ ] 3.3 The full suites, each alone and read by its exit code: Compiler, Conformance, Web, Server, and the runtime's `TestRuntime`
