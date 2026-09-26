# Tasks

## 1. The constructor owns the defaults

- [x] 1.1 Convert each member's declared default in the record emitter, under the twin's module
- [x] 1.2 Pass `undefined` for a skipped member at construction sites, and drop `ValueMember.Default`
- [x] 1.3 Read the primary constructor's parameters as bare names in a default and in a base clause
- [x] 1.4 Check: `RecordInitializerConformanceTests` run on both sides, 19 of the first 23 failing before

## 2. From the review

- [x] 2.1 Register every struct a default's zero constructs, in the record, component and plain-class emitters
- [x] 2.2 Check: the four sites' import test fails without the change, and a struct's initializers run on both sides

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures record row in English and Portuguese, published at the merge
- [x] 3.2 One `docs/LEDGER.md` line citing #385
- [x] 3.3 Archive this change in the same pull request
