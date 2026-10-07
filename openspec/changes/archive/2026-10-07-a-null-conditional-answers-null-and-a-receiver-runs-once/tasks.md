# Tasks

## 1. A null-conditional read answers null

- [x] 1.1 `ConditionalAccessStrategy` answers `(chain ?? null)` where the value is used, and leaves the chain bare for a call that returns nothing, a statement, the left of a `??` and the tail of another chain; proved by `NullConditionalTailTests.AChain_AnswersNullWhereItsValueIsUsed_AndOnlyThere`
- [x] 1.2 Run both sides through the conformance harness: `NullConditionalValueConformanceTests`, the dictionary that looks for null failing on main
- [x] 1.3 A guard settles a tail that is a chain of its own; proved by `NullConditionalTailTests.AChainBehindAGuard_AnswersNullWhereItsValueIsUsed` and the conformance case "a chain behind a guard", failing before the fix with both annotations

## 2. A method group reads its receiver once

- [x] 2.1 `MemberAccessStrategy` binds a method group through a template over its receiver; proved by `MethodGroupConformanceTests`, its receiver case failing on main with .NET 2 and JavaScript 4
- [x] 2.2 A group on `base` binds `this`, and an extension's group goes to the home `InvocationStrategy.ExtensionHome` names, the decision its call takes; proved by `MethodGroupTests` and the conformance cases on base and on an extension, failing before the fix
- [x] 2.3 A group over an extension nothing emits fails the build with EQ2004; proved by `MethodGroupTests.AnExtensionGroupNothingEmits_FailsTheBuild_AsItsCallDoes`

## 3. Proof and documentation

- [x] 3.1 The twins regenerated with `EQ_UPDATE_TRANSPILED=1`, the served runtime measured with the Server suite's own budget test
- [x] 3.2 Run the five suites and build `samples/DefaultUIDashboard` with no warning
- [x] 3.3 The wiki's Compiler page, in English and Portuguese, on a wiki branch named like this pull request's
- [x] 3.4 One `docs/LEDGER.md` line citing #633, #619 and #655
