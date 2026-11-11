# Tasks

## 1. Two runtimes

- [x] 1.1 The boot reaches the dev loop through one seam, and `boot.dev.ts` installs it. Verify: `runtime.js`'s map names no `src/dev/` source, and `runtime.dev.js`'s names the overlay and the hot-reload state
- [x] 1.2 The server names the runtime in the import map and answers the dev one only where it serves the loop. Verify: `ServedDevLoopTests`, over Production, Development, and Production with hot reload
- [x] 1.3 The hot-reload spec boots the dev runtime. Verify: the runtime's `tsc` and vitest, 203 files and 2958 tests

## 2. Proved against the real thing

- [x] 2.1 Measure both bundles. Verify: `runtime.js` 1,272,696 bytes and 315,199 gzip, `runtime.dev.js` 1,303,187 and 324,027
- [x] 2.2 Hot reload and the error overlay under the dev runtime. Verify: the web edit-to-view measurement hydrated 4 of 4 saves on the dashboard sample with the page loading runtime.dev.js, and in Development an uncaught error opened the overlay (`#equantic-error-overlay`) on a page whose only runtime was runtime.dev.js
- [x] 2.3 The served runtime's budget records it. Verify: `ServedRuntimeBudgetTests` regenerated, 324,551 bytes of gzip to 315,740, in a commit of its own; measured again over the main of 11-11, whose runtime had grown to 326,325, it records 317,904

## 3. Documentation

- [x] 3.1 CLAUDE.md's bundle strategy, and the wiki's Runtime and BuildFlow pages in every language they are written in
- [x] 3.2 One entry for the ledger citing #1359, as a file of its own under `docs/ledger/`
