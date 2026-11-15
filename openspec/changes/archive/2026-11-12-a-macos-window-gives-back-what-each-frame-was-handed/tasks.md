# Tasks

## 1. Measure

- [x] 1.1 On the runner, say how far an edit the window did not draw got: the app's line as the edit arrives, and the measurement's failure quoting dotnet watch and the app after the save
- [x] 1.2 Eleven rounds of the desktop measurement on the macOS runner, each A/B three times: the app alone, `dotnet watch` without edits, the runtime's modifiable mode, the loop's pool, the host's caches kept across an edit, Spotlight's indexing off, and the load of the build let fall before the first edit
- [x] 1.3 Run the desktop sample with `OBJC_DEBUG_MISSING_POOLS=YES` for 120 and 480 frames, with the loop's pool and without it, and count what each frame leaves behind by class

## 2. The fix

- [x] 2.1 Each turn of the macOS window's loop in a pool of its own, popped on every way out of the turn
- [x] 2.2 The camera's `startRunning` and `stopRunning`, on a pool thread, inside a pool
- [x] 2.3 `scripts/check-frame-pools.sh`, run by the macOS samples job: counts on the loop's thread, told apart as the one that reports the NSWindow, so the frameworks' own queues are not counted; fails on the loop without the pool (244 left over 122 frames, a drawable and a render pass descriptor each) and passes with it (0)

## 3. Documentation

- [x] 3.1 One ledger entry citing the issues, as a file of its own under `docs/ledger/`
