# Tasks

## 1. Add refuses a key already there (#440, #395)

- [x] 1.1 Give the runtime's dictionaries an `add` that refuses in each collection's measured words, seed the constructors through it, and write an object initializer's entries through `assign`. Verify: `dictionary.spec.ts`, and `DictionaryAddConformanceTests.Add_RefusesAKeyAlreadyThere_TheIndexerReplaces` on both sides, failing against main
- [x] 1.2 Lower `Add` to `add`, and keep a collection initializer (added) apart from an object initializer (assigned). Verify: the same cases

## 2. A pair built by the app (#433)

- [x] 2.1 Lower `new KeyValuePair<K, V>(…)`, `KeyValuePair.Create` and the parameterless constructor to the runtime's pair, by the bound symbol, and its default to the pair of the defaults. Verify: `DictionaryAddConformanceTests.AKeyValuePairBuiltByTheApp_IsThePairADictionaryYields` on both sides, failing against main

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures page in English and Portuguese, on the pull request's wiki branch, and one `docs/LEDGER.md` line citing #440, #395 and #433
