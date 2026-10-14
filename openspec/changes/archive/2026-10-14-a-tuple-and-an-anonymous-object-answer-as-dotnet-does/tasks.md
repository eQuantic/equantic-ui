# Tasks

## 1. Measure before changing

- [x] 1.1 Write `TupleAndAnonymousObjectConformanceTests`, the whole family: an anonymous object's `Equals` (equal, a different value, another member set, null, a record), `==`, `GetHashCode` and text, and a tuple's text through every route into text, in the invariant culture, pt-BR and sv-SE, its `Equals` and `GetHashCode`
- [x] 1.2 Run them against 85b0ab52: 63 of the 69 cases fail, and the six that pass are what already answered as .NET

## 2. The runtime

- [x] 2.1 `$eq.text.tuple` and `$eq.text.tupleGroup`, `$eq.text.record` with an empty name for an anonymous object, with vitest cases
- [x] 2.2 `$eq.equalsGroup` taking the comparison a type compares by, and `memberEquality` taking only an anonymous object of its own members in their order, with vitest cases

## 3. eqc

- [x] 3.1 `StringConversion` writes a tuple's and an anonymous object's text from the static type, and a record's member of either type through it; an aligned or a formatted hole and `string.Format` hand the formatter that text
- [x] 3.2 `StructuralEqualsStrategy` (moved to the IR) and `ElementEquality.OwnEquals` choose an anonymous object's and a `Nullable` tuple's `Equals`, and `MemberAccessStrategy` makes their `Equals` and `ToString` method groups
- [x] 3.3 `ValueCopies` takes no copy of a tuple boxed into text, around the text it became
- [x] 3.4 Pin what each lowering hands the runtime (`TupleAndAnonymousObjectLoweringTests`)
- [x] 3.5 Prove it both ways: with main's files the new rows fail, with a fix that sends an anonymous object's `Equals` to `$eq.equals` three of them fail, and with this one all pass; the conformance, compiler, Server and Web suites and the runtime's tsc and vitest pass

## 4. Regenerated and documented

- [x] 4.1 The shared components' modules (`EQ_UPDATE_TRANSPILED=1`), the IR baseline, and the declared public API
- [x] 4.2 The wiki's SupportedFeatures page, in English, Portuguese, Spanish, French and German, on the wiki branch of the same name
- [x] 4.3 One ledger entry citing the issues, as a file of its own under `docs/ledger/`
