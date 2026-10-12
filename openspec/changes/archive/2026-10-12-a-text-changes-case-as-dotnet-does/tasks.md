# Tasks

## 1. Measure before changing

- [x] 1.1 Measure .NET 10's casing over every code unit of the BMP and every code point of the first supplementary plane, in the invariant culture and in tr, az and the common cultures, against the engine's full mappings, and list the code points an engine of a later Unicode cases and .NET 10 does not
- [x] 1.2 Write `CasingConformanceTests` and run them against 9b43eb2b: 72 of the 96 cases, in eight cultures, fail as #848 and #725 describe

## 2. The twins and the calls

- [x] 2.1 Add `utils/casing.ts` with its vitest cases, exported as `$eq.text.toUpper` and `$eq.text.toLower`, and read its invariant upper case for the ordinal comparison that ignores case
- [x] 2.2 Lower `ToUpper` and `ToLower` and their invariant pair, of a string and of a char, to the twins in eqc, the culture read by `NamedCulture.ReadsInvariant`, and refuse a char's casing with a culture taken as a delegate
- [x] 2.3 Refuse `new DateOnly(…, calendar)` at the build, as `DateTime`'s and `DateTimeOffset`'s
- [x] 2.4 Prove it both ways: the new conformance cases pass and the whole suite with them, the compiler's 17 new cases fail without the change, the null-argument baseline only shrinks, the shared components' pinned modules do not change, and the compiler, Web and Server suites and the runtime's tsc and vitest pass

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures page, in English and Portuguese, on the wiki branch of the same name
- [x] 3.2 One `docs/LEDGER.md` line citing the issues, and EQ2108's row in `docs/DIAGNOSTICS.md`
