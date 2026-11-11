## 1. An app's Equals

- [x] 1.1 Ask an app's `Equals` every time .NET asks it, a value compared with itself included, keeping the reference check of a record's synthesized `Equals` only.
- [x] 1.2 Refuse with EQ2010 a comparison that reaches a `[ServerOnly]` `Equals` through a collection, a tuple, an anonymous object or a contract.
- [x] 1.3 Reach the method an `IEquatable<U>` call binds in generic code.

## 2. A generic value's closed type

- [x] 2.1 Keep a generic record's or struct's closed type where generic code builds it, at its zero and when it is rebuilt from the wire.

## 3. Delegates

- [x] 3.1 Compare two delegates by target and method, and combine a plain delegate as `Delegate.Combine` does.

## 4. Tests

- [x] 4.1 `AnAppsEqualityConformanceTests`, `DelegateEqualityConformanceTests` and `GenericRecordClosureConformanceTests` on both sides, `ServerOnlyEqualityTests` for the fence, and the runtime's `delegates.spec.ts`, `key-equality.spec.ts` and `hydrate.spec.ts`.

## 5. Documentation

- [x] 5.1 The wiki's SupportedFeatures page, in every language it is written in.
- [x] 5.2 A ledger entry under `docs/ledger/`.
