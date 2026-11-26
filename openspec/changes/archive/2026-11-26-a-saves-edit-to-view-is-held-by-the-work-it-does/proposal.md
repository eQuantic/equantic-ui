# Proposal

Closes #1909, a Bug under #1587.

## Why

The `samples` job held the web's edit-to-view to a ceiling of 1.5s on the warm median of three saves. On loaded macOS runners the median moved with the load: 1509 and 1898ms on queue runs whose fastest saves were 1471 and 1074ms. The merge queue dropped pull requests that never touched hot reload or the web, and a re-queue went green only when the next runner happened to be quieter. A third run failed before any save with "dotnet watch exited before the app answered", which named no cause. The measurement is #900's instrument, so it could not just be loosened: a ceiling a loaded runner passes is one a real regression passes too.

## What Changes

- **eqc counts the work of each compile** in `EQC_TIMINGS`:
  - whether it kept the compilation the compile before it loaded;
  - the files it walked and compiled;
  - the TypeScript modules it wrote;
  - the files and bytes bun wrote.
- **A save is held to that work.** The budget's `work` holds the most any warm save did, so a save that compiles, writes or bundles more than one screen's worth fails whatever the clock says. A warm save that loaded the project cold fails too.
- **The time is held on the fastest warm save** instead of the median: a loaded runner slows some saves and never makes one faster. The ceilings stay as they were, and the median is still reported.
- **A `dotnet watch` that exits before the app answers is started once more.** Its exit says what it said last, its exit code and the load, and a second exit fails the run with the same.
- **The budget's comment** says what the hold is now and why.

## Impact

- `eQuantic.Build` (eqc's `EQC_TIMINGS`).
- `eQuantic.UI.Compiler`'s `AppBuild` and `ModuleBundler`: counts, a tool surface.
- `scripts/measure-edit-to-view.cs` and `scripts/edit-to-view.budget.json`.
- `openspec/specs/hot-reload`.
