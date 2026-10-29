# Tasks

## 1. eqc

- [x] 1.1 `LookupStrategy` replaces `LookupIndexerStrategy`: one search over the groups by the key type's equality
      (`LinqKeys.Matches`), `find` for the indexer and `some` for the lookup's own `Contains(key)`, which it claims
      by the bound method, above `ContainsStrategy`
- [x] 1.2 The registration in `CSharpToJsConverter`, the compiler's `PublicAPI.Unshipped.txt`, and the
      introduced-functions baseline, whose entry moves with the file at the same count and is judged

## 2. The proof

- [x] 2.1 `CollectionTailConformanceTests.LookupContains_MatchesDotNet`, executed on both sides: a string key, an
      element selector, a record key, a NaN key, a null key, a key named like an inherited member, a key that
      counts its calls, and `?.` over a null lookup and a lookup; and a class key in
      `LinqKeyEqualityConformanceTests`, by its own `Equals`, its subclass's and by reference. Every case that
      finds a key fails before the change and passes after
- [x] 2.2 The lookup indexer's conformance cases, the transpiled pins and the compiler's coverage suites pass
      unchanged

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures page, in the five languages it is written in: a lookup's `Contains` by its
      key, and the LINQ row no longer says the indexer is not modelled
- [x] 3.2 One ledger entry citing #1145, as a file of its own under `docs/ledger/`
