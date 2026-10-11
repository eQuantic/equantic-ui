# Tasks

## 1. Measure before changing

- [x] 1.1 Measure every overload of the four encodings and the five `TryParse(string, out T)` on .NET 10, and read `Convert.cs` for the order of its checks and the line breaks
- [x] 1.2 Write `ConvertEncodingConformanceTests` and `DateTimeTryParseConformanceTests`, and run them against 20069215: 78 of the 81 cases fail as #716 and #718 describe

## 2. The twins and the calls

- [x] 2.1 Add the four encodings to the new `utils/convert-encoding.ts` and `$eq.convert`, and `tryParse` to the five time factories, with their vitest cases; `TimeOnly.Parse` reads the whole text and a clock that exists
- [x] 2.2 Hand each encoding to its twin by the bound overload, a span that is not a collection expression refused, and by the argument count where no model can be asked; lower the five `TryParse(string, out T)` inline over the twins' readers and refuse their other overloads at the build; let the DatePicker say `DateOnly.TryParse`
- [x] 2.3 Prove it both ways: the new conformance cases pass and the whole suite with them, the null-argument baseline only shrinks, by 35 (17 answered, 18 refused at the build), the shared components' pinned modules change only in the DatePicker's parse, and the compiler, Web, Server and Design suites and the runtime's tsc and vitest pass

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures page, in English and Portuguese, on the wiki branch of the same name
- [x] 3.2 One `docs/LEDGER.md` line citing the issues
