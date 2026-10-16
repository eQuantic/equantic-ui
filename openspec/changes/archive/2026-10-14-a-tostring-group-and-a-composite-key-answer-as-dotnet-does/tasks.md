# Tasks

## 1. Measure before changing

- [x] 1.1 Write the rows: the `ToString` method group of every type whose text its type writes, and a key or an element of every LINQ operator that holds one, by a tuple, an anonymous object, a record, one holding an array, a decimal, a date, a double, a class that overrides `Equals`, one that does not, a subclass behind its base and a record behind an interface
- [x] 1.2 Run them against dfce1158: 19 of the 21 fail, and the two that pass are what already answered as .NET

## 2. The runtime

- [x] 2.1 `$eq.text.group`, the method group writing a function of the value it was made with

## 3. eqc

- [x] 3.1 `LinqTableStrategy` holds a join's and a set operator's keys in the runtime's dictionary and set by `ElementEquality`, and `DistinctStrategy` reads it too
- [x] 3.2 `LinqKeys.Matches` writes the key type's comparison for `GroupBy`, `ToLookup` and the lookup indexer, with `===` only where that answers the same
- [x] 3.3 `MemberAccessStrategy` makes every `ToString` method group through `StringConversion.TextGroup`
- [x] 3.4 Prove it both ways: with main's files 19 of the 21 rows fail, with the first version of this fix, which held by value only what `LinqKeys.ComparesByValue` names, 7 fail, and with this one all pass; the conformance, compiler, Server and Web suites and the runtime's tsc and vitest pass

## 4. Regenerated and documented

- [x] 4.1 The shared components' modules (`EQ_UPDATE_TRANSPILED=1`), the hand-written functions baseline, and the declared public API
- [x] 4.2 The wiki's SupportedFeatures page, in English, Portuguese, Spanish, French and German, on the wiki branch of the same name
- [x] 4.3 One ledger entry citing the issues, as a file of its own under `docs/ledger/`
