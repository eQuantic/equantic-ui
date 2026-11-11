# Tasks

## 1. The completion asks again

- [x] 1.1 `CodeCompletion` asks again, as the list opened, each provider whose complete answer's word the word no longer extends. Verify: `CodeCompletionTests.ACompleteAnswer_IsAskedForAgain_OnceTheCaretGoesBackPastItsWord` and `GoingBack_AsksOnlyTheProvidersWhoseWordWasLeft_AsTheListOpened`, both failing with the rule removed, and `CodeCompletionTwinTests.GoingBackPastTheWordOfACompleteAnswer_AsksAgainAlikeOnBothSides`

## 2. A problem of no width keeps its place

- [x] 2.1 `CodeEdit.Carry` keeps an empty range in front of an insertion that begins with a line break. Verify: `CodeLinesTests.AProblemOfNoWidth_AtAMovedLinesEdge_StaysOnIt` with the two moves past the document's end, and `AProblemOfNoWidth_AtALinesEnd_StaysThereWhenEnterIsTypedAtIt`, both failing with the rule removed

## 3. The test that raced its dispatcher

- [x] 3.1 `CodeDiagnosticsTests.AnAnswerFromAnotherThread_IsCountedAndAppliedOnTheUiThread` drains what the editor posted as it was made before waiting. Verify: measured, the queue held one item before the answer in 300 of 300 runs and the answer posted after the await in 1 of 300; the fixed test passes 100 of 100 runs at load 150 to 187

## 4. Documentation

- [x] 4.1 One entry for the ledger citing the three issues, as a file of its own under `docs/ledger/`
