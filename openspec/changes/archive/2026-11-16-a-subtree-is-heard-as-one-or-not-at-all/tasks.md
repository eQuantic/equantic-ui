## 1. The vocabulary

- [x] 1.1 `MergeSemantics` and `ExcludeSemantics` (`Excluding`, true by default) in `src/eQuantic.UI.Primitives/Nodes`, `IVisualNodeVisitor` members, `ChildSlots` arms, `PublicAPI.Unshipped.txt`
- [x] 1.2 `UI.MergeSemantics(child)` and `UI.ExcludeSemantics(child, excluding)` in `eQuantic.UI.Components`

## 2. Photon and the email walk

- [x] 2.1 `MeasureVisitor`: both measured as wrappers; `EmitVisitor`: both descend
- [x] 2.2 `SemanticsVisitor.Merging.cs`: the merge collects and joins its stops (design, decision 2); the exclusion consumes its subtree
- [x] 2.3 `EmailWalk`: both reach a message as their child; `EmailRefusalParityTests` carries them

## 3. The web and its TypeScript twin

- [x] 3.1 `WebLoweringVisitor.Semantics.cs` and `MergedReading.cs` (design, decisions 3 to 5); `TextContentOf` and `CapsAt` arms
- [x] 3.2 `lowering.ts` (`lowerMergeSemantics`, `lowerExcludeSemantics`, `mergeReading`, the `capsAt`, `fills` and `visualTextOf` cases), `nodes.ts`, `vocabulary.ts`, exports; `node-kinds.generated.ts`, `ChildSlots.ts` and `UI.ts` regenerated
- [x] 3.3 `component-parity` fixture: four cases on both producers, recorded with `EQ_UPDATE_PARITY_FIXTURE=1`

## 4. The harness and the proof

- [x] 4.1 `AnnouncedTree` reads `aria-labelledby` as accname does (design, decision 7)
- [x] 4.2 `SemanticsParityTests`: seven curated cases, alike on both targets
- [x] 4.3 `MergeAndExcludeSemanticsTests`: what each target says, the merged stop pressed by the bridge and by a tap, the exclusion leaving a control pressable
- [x] 4.4 Each proof fails with its arm sabotaged, in a disposable worktree
- [x] 4.5 `docs/FLUTTER-PARITY.md` row SAME, `FlutterParityPinTests` probe flipped; the wiki's semantics pages in five languages
