# Tasks

Every build and test runs through the host's lock, `python3 scripts/one-build-at-a-time.py -- <command>`,
with `EQ_WIKI_DIR` set as the delivery gives it.

## 1. The net, before the refactor

- [x] 1.1 Run the nets on the tree as it is and record each count: the Web suite's
  `RoleBearingHostBoundsTests`, `ProgressSemanticsTests`, `AdjustableValueTests`,
  `LiveRegionSemanticsTests`, `GroupSemanticsTests`, `UnlabelledGroupTests` and
  `ComponentParityFixtureTests`, and the runtime's `role-host-bounds`, `progress`, `adjustable`,
  `live-region`, `select-groups`, `merge-semantics` and `component-parity` specs
  (`dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`). Verify: all green, the counts written in the
  pull request
- [x] 1.2 Add the `MergeSemantics` and `ExcludeSemantics` rows to `RoleBearingHostBoundsTests` and to
  `role-host-bounds.spec.ts`. Their hosts bear no role, so the row's `role` assertion is the one thing
  that does not apply to them: assert the width contract only for those two rows. Verify: the new rows
  pass on the tree as it is, and fail when `SemanticsHost` / `semanticsHost` writes a bare `div`
- [x] 1.3 Mutate the C# host of one wrapper at a time to drop `MaxWidth`, then `Height`, and the TS host
  likewise; note which net fails for each. Where no net fails, add the assertion (the cap passes through
  as `max-width`, a filling child's height as `height: 100%`) to both bounds suites, with a capped and a
  filling child. Verify: every one of the four mutations fails at least one suite on each producer, and
  the tree as it is passes

## 2. C#: one host on the server

- [x] 2.1 Widen `SemanticsHost` in `src/eQuantic.UI.Web/WebLoweringVisitor.Semantics.cs` to
  `(VisualNode child, Dictionary<string, string> attributes, string? label, bool takesPointer)`, as
  design.md writes it, and move the width contract's comments there, citing #239, #241 and the two
  suites. Change `LowerMergeSemantics` and `LowerExcludeSemantics` to the new shape. The one-argument
  shape goes
- [x] 2.2 Send `LowerProgress`, `LowerAdjustable`, `LowerLiveRegion` and `LowerGroup`
  (`WebLoweringVisitor.Interaction.cs`) through it, `Adjustable` with `takesPointer: true`, and delete
  each one's own `Fills`, `CapsAt`, `HtmlStyle` and `aria-label` lines. Verify: the Web suite whole
  passes with the counts of 1.1 plus the rows of 1.2 and 1.3, the component pins hold with no
  `EQ_UPDATE_*` set, and `git grep -n '"fit-content"' src/eQuantic.UI.Web` names only `SemanticsHost`,
  `LowerSheetSurface` and the `Containers.cs` site

## 3. TypeScript: the twin in the browser

- [x] 3.1 Widen `semanticsHost` in `src/eQuantic.UI.Runtime/src/shared/lowering.ts` to
  `(child, attributes, label, takesPointer)`, keeping the style's key order (width, max-width,
  pointer-events, height), and change `lowerMergeSemantics` and `lowerExcludeSemantics` to it. Its
  comment cites the C# twin
- [x] 3.2 Send `lowerProgress`, `lowerAdjustable`, `lowerLiveRegion` and `lowerGroup` through it,
  `lowerAdjustable` with `takesPointer` true, and delete each one's own copy. Verify:
  `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime` passes whole (tsc, lint and vitest) with the
  counts of 1.1 plus the new rows, and `grep -n "'fit-content'" lowering.ts` names only `semanticsHost`,
  `lowerSheetSurface` and the shifted wrapper

## 4. Proved against the real thing

- [x] 4.1 The parity net executed on both sides: `ComponentParityFixtureTests` and
  `component-parity.spec.ts` against the same fixture, with `component-parity.fixture.json` unchanged
  in the diff. Verify: both green, and `git diff --stat` shows no pin, fixture or baseline file
- [x] 4.2 `dotnet build samples/DefaultUIDashboard`, served, and a page holding a progress bar, a
  slider, a toast (live region) and a grouped select opened in a browser. Verify: the server's markup
  of each host and the hydrated DOM's are the same element, with the same classes, and the console shows
  no hydration mismatch
- [x] 4.3 The served runtime's budget test passes with no delta file written (the bundle only shrinks)

## 5. Documentation

- [x] 5.1 One entry for the ledger citing #1517, as a file of its own under `docs/ledger/`. No wiki page
  describes these hosts, so no wiki branch is owed
- [x] 5.2 Archive the change with `scripts/openspec.sh archive a-role-host-is-written-by-one-helper --yes`
  before the merge, and run `./scripts/check-openspec.sh`. Verify: it passes, and `openspec/specs`
  does not move (`skip_specs`)
