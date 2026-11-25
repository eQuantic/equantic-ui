# Proposal

Closes #1876, a Bug under #1587.

## Why

A pan on a table viewport arms a capturing click guard on the window, so the click a browser sends where the pan ended
reaches nothing, and removed it 400 ms later on a bare timer that nothing cancelled. In the runtime suite, a spec's page
was torn down before the 400 ms passed, the timer fired against a window that no longer had its methods
(`target.removeEventListener is not a function`), and vitest failed a run in which every file had passed: a known
flake, rerun until green, which cost a CI round each time and once dropped a pull request from the merge queue. In an
app, a table unmounted within 400 ms of a pan kept its guard alive until the timer fired.

## What Changes

- **The guard is the table's.** It is kept on the table's element and let go at once, timer and listener, when the
  table stops being watched: an unmount and a disposed position both reach that point (`stopWatching`).
- **The click it waited for takes it.** When the guard stops the click it was armed for, it goes with its timer, so
  no timer of it is left waiting.
- **Every table the commit adopts is watched**, whether or not a `ResizeObserver` follows its size: it was watched
  only through its observer, so where there is none an unmount never reached the guard.
- **The specs let go of what they leave**: the table spec after each case, and the runtime's spec setup after each
  file (`test-runtime.ts`), so a file whose last case pans a table leaves no timer behind it.

A developer writes nothing different. No public surface and no developer surface moves.

## Impact

- `eQuantic.UI.Runtime`: `table-viewports.ts`, its spec and the spec setup (`test-runtime.ts`).
