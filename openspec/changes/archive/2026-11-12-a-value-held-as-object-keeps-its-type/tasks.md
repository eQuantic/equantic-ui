## 1. The box

- [x] 1.1 Box an enum member and a float at every boxing conversion into `object` or `ValueType` (`Boxing`, `ValueFlow.Apply`), with a runtime box that answers `Equals`, `GetHashCode`, `ToString` and JSON (`utils/box.ts`).
- [x] 1.2 Unbox at the casts, the patterns, the switch over `object`, `Convert`, the `Enum` statics, and box the receiver of an enum's and a float's `Equals(object)`.

## 2. The marks

- [x] 2.1 Mark a list and an array converted to `object`, and an anonymous object where it is made and copied (`utils/marks.ts`); `equals`, `sameKey` and the hash read the marks.

## 3. Tests

- [x] 3.1 Conformance tests (`BoxedValueConformanceTests`): each row of the four issues, every unboxing site, both sides executed; runtime specs for the box and the marks.

## 4. Documentation

- [ ] 4.1 The wiki's SupportedFeatures page, with the limits, and the Upgrading page, in every language they are written in.
- [ ] 4.2 A ledger entry under `docs/ledger/`.
