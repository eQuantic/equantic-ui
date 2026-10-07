# Tasks

## 1. Each collection writes the JSON .NET writes

- [x] 1.1 `toJSON` on `Queue`, `Stack`, `LinkedList` and `SortedSet`, in the order each enumerates
- [x] 1.2 Prove it on both sides in `CollectionJsonConformanceTests`: each collection returned, nested and after it changed, failing on the base

## 2. A dictionary's views are live, and its capacity is .NET's

- [x] 2.1 Measure .NET's `EnsureCapacity`, `TrimExcess`, a capacity constructor and a view read after a change
- [x] 2.2 `Keys` and `Values` as live views, read-only and walked under the dictionary's version; the keys' own `Contains`
- [x] 2.3 `EnsureCapacity` and `TrimExcess` over .NET's primes, and the capacity a constructor is given
- [x] 2.4 Prove it on both sides in `DictionaryViewConformanceTests`, failing on the base

## 3. A member compared by its type's default comparer

- [x] 3.1 `ElementEquality.Compare`, and a record's members and a tuple's `Equals` compared through it
- [x] 3.2 Prove it on both sides in `ArrayMemberEqualityConformanceTests`, failing on the base

## 4. Documentation and the suites

- [x] 4.1 The wiki's SupportedFeatures page, English and Portuguese, on the wiki branch of this pull request
- [x] 4.2 `docs/LEDGER.md`: one line citing #597, #463 and #554
- [x] 4.3 The full suites, each alone and read by its exit code: Compiler, Conformance, Web, Server, and the runtime's `TestRuntime`
