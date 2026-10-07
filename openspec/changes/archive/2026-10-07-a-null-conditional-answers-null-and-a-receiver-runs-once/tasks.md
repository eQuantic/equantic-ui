# Tasks

## 1. A null-conditional read answers null

- [x] 1.1 `ConditionalAccessStrategy` answers `(chain ?? null)` where the value is used, and leaves the chain bare for a call that returns nothing, a statement, the left of a `??` and the tail of another chain; proved by `NullConditionalTailTests.AChain_AnswersNullWhereItsValueIsUsed_AndOnlyThere`
- [x] 1.2 Run both sides through the conformance harness: `NullConditionalValueConformanceTests`, the dictionary that looks for null failing on main

## 2. A method group reads its receiver once

- [x] 2.1 `MemberAccessStrategy` binds a method group through a template over its receiver; proved by the same conformance class, its method group case failing on main with .NET 2 and JavaScript 4

## 3. Proof and documentation

- [x] 3.1 The twins regenerated with `EQ_UPDATE_TRANSPILED=1`, the served runtime measured with the Server suite's own budget test
- [x] 3.2 Run the five suites and build `samples/DefaultUIDashboard` with no warning
- [x] 3.3 The wiki's Compiler page, in English and Portuguese, on a wiki branch named like this pull request's
- [x] 3.4 One `docs/LEDGER.md` line citing #633 and #619
