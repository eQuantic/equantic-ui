# Tasks

## 1. eqc

- [x] 1.1 `LookupStrategy` claims `System.Linq.Lookup<TKey, TElement>` beside `ILookup<TKey, TElement>`, for its
      indexer and its own `Contains(key)`
- [x] 1.2 `ContainsStrategy` takes a `System.Linq` `Contains` only when the method is an extension, in `CanConvert`
      and where `Convert` reads the call's source as LINQ's

## 2. The proof

- [x] 2.1 `CollectionTailConformanceTests.ConcreteLookup_MatchesDotNet`, executed on both sides: `Contains` and the
      indexer over a cast, a record key, a type pattern, an `as`, and `?.` over a null concrete lookup and a
      lookup. All six fail before the change and pass after
- [x] 2.2 With `LookupStrategy` no longer claiming `Contains`, a lookup's `Contains` is EQ2004 at build rather than an
      element search, measured once and restored
- [x] 2.3 The interface's lookup cases, the key-equality cases, the transpiled pins and the compiler's coverage
      suites pass unchanged

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures page, in the five languages it is written in: the concrete `Lookup<,>` beside
      the interface
- [x] 3.2 One ledger entry citing #1168, as a file of its own under `docs/ledger/`
