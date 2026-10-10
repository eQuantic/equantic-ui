# Tasks

## 1. The mark

- [x] 1.1 `$eq.closing` and `$eq.sameClosure` in the runtime, by a WeakMap, and `withPatch` carrying the
      mark to its copy
- [x] 1.2 `ObjectCreationStrategy` marks a generic record or struct the source declares, closed over
      types the build knows
- [x] 1.3 `RecordTypeEmitter` writes the comparison into a generic record's or struct's `equals`
- [x] 1.4 Check: `GenericRecordClosureTests` pins both shapes, and a record without type parameters
      unchanged

## 2. Against the real thing

- [x] 2.1 `GenericRecordClosureConformanceTests`, both sides executed, nine cases with and without
      type annotations: the five across type arguments (explicit, `with`, a list, a struct and a
      target-typed `new`) fail on main and pass here, and the four of one closed type (two `Box<int>`,
      a value built in generic code, a named tuple and `dynamic`) pass on both. The tuple and `dynamic`
      cases fail on the first mark, which kept the type arguments' display text
- [x] 2.2 The wiki's SupportedFeatures in English and Portuguese, and one `docs/LEDGER.md` line
