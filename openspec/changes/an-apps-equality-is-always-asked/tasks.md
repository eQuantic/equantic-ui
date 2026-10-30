## 1. An app's equality

- [x] 1.1 Ask a value's own `Equals` in `sameItem` and `equals`, past the identity check, with a record's synthesized one kept.
- [x] 1.2 Choose a contract over an erased type argument by the other value.

## 2. The fence

- [x] 2.1 Move `Refuses` to `ElementEquality`, walking a value's parts.
- [x] 2.2 Fence every comparer a call writes, and an instance call through a contract.

## 3. Delegates

- [x] 3.1 Write `boundDelegate` and `delegateOver`, and route a method group and a delegate built from another.
- [x] 3.2 Route a delegate's `+=`, `-=`, `+`, `-` and `Equals`.

## 4. Closed types

- [x] 4.1 Mark a generic method's answer at its call, a struct's zero, and a value from the wire.

## 5. Tests

- [x] 5.1 `AnAppsEqualityConformanceTests`, `DelegateEqualityConformanceTests`, `GenericRecordClosureConformanceTests`, `ServerOnlyEqualityTests`, and the runtime specs.
