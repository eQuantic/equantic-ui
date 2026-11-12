# Tasks

## 1. The instrument says why

- [x] 1.1 The server writes why a rebuild tells the page to hydrate or to mount. Verify: the announcement test reads the line after "rebuilt in"
- [x] 1.2 The measurement records each document's frames, what the reload carried and whether the server rendered it, and a draw-over fails naming them. Verify: the script builds

## 2. One announcement per save

- [x] 2.2 An event for a path already gone dates nothing and opens no rebuild. Verify: `AnEventOfAPathAlreadyGone_DoesNotDateTheSaveAfterItsEdit`, in both orders, and `AnEventOfAPathAlreadyGone_AfterTheSavesRebuild_AnnouncesNothing` fail before (mount) and pass after
- [x] 2.1 A rebuild with no save since the last does nothing. Verify: `ASecondCallForTheSaveTheLastRebuildAnswered_AnnouncesNothing` fails before (two announcements, the second a mount) and passes after, with the 29 hot-reload tests

## 3. Proved against the real thing

- [x] 3.1 The web edit-to-view measurement, locally at a load near 50. Verify: 30 saves in five rounds, 29 hydrated; the one that drew over had every server-side signal saying hydrate, before the boot said why, and 18 of 18 hydrated once it did. On the `samples` job the boot then said nothing on the saves the measurement failed, while every server-side signal said hydrate
- [x] 3.2 The measurement reads the boot's verdict once the boot gives one, never off the snapshot the edit's text first appeared in, which the server's markup puts on screen before the page's module arrives. Verify: with 400 ms of latency in the browser, the old reading failed 4 saves of 4 as drawn over while the boot hydrated them, and the new one passed 4 of 4; with none, both pass

## 4. Documentation

- [x] 4.1 One entry for the ledger citing #1373, as a file of its own under `docs/ledger/`
