# Tasks

## 1. The receiver binds in its function

- [x] 1.1 Add the temporaries to the conversion context: fresh names per module, a scope per statement or body, and the node cache's rule for a translation that names one; proved by `NullConditionalTailTests.AStatementConvertedAgain_DeclaresTheTemporaryItsTranslationNames`
- [x] 1.2 Declare what a statement bound in front of it, leaving a labeled loop's to its label; proved by `ABodyWrittenWithoutBraces_GetsTheBracesItsDeclarationNeeds`, `TwoStatements_EachDeclareTheirOwn` and `CSharpVersionCoverageTests.ALabeledLoopWhoseConditionBindsATemporary_DeclaresItInFrontOfTheLabel`
- [x] 1.3 Declare what a concise body bound in its block, and give a concise lambda a block only when its body bound one; proved by `AConciseLambda_ThatBindsATemporary_DeclaresItInABlockOfItsOwn`, `AnAsyncConciseLambda_AwaitsInItsOwnBlock` and `AConciseLambda_ThatBindsNothing_StaysAnExpression`
- [x] 1.4 Assign a receiver that is not read again to its temporary in `ConditionalAccessStrategy`, keeping the arrow only where no statement can declare one, and drop the EQ1004 refusal; proved by `ACallReceiver_WithATailThatAwaits_IsBoundToATemporary_AndTheAwaitStaysInTheMethod` and `ANullConditionalInTheTailOfAnother_BindsATemporaryOfItsOwn`
- [x] 1.5 Run both sides through the conformance harness: `NullConditionalChainConformanceTests.BehindAReceiverThatIsNotALocal_TheTailRunsOnceAndOnlyWhenItShould` (10 cases) and `AsyncConformanceTests.AnAwaitBehindANullConditionalOnAnyReceiver_RunsAsTheGuardSays` (6 cases); the 6 async cases fail on main, and the labeled loop fails with the label rule removed (bun: `Cannot "continue" to label outer`)

## 2. What the change moves

- [x] 2.1 Regenerate the component library's twins (`EQ_UPDATE_TRANSPILED=1`): `DataTable.ts` is the one that moves, its selection test bound to `$n0`
- [x] 2.2 Record the arrow that stays in `introduced-functions.baseline.txt` under the new reason "no statement there"
- [x] 2.3 Run the five suites (Compiler, Web, Server, Conformance, the runtime's `-t:TestRuntime`) and build `samples/DefaultUIDashboard` with no warning

## 3. Documentation

- [x] 3.1 The wiki's Compiler page, in English and Portuguese, on a wiki branch named like this pull request's: an awaited argument behind `?.` is awaited where it is written whatever the receiver is, since 0.2.0-preview.61
- [x] 3.2 One `docs/LEDGER.md` line citing #539
