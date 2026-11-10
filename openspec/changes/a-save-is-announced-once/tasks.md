# Tasks

## 1. The instrument says why

- [x] 1.1 The server writes why a rebuild tells the page to hydrate or to mount. Verify: the announcement test reads the line after "rebuilt in"
- [x] 1.2 The measurement records each document's frames, what the reload carried and whether the server rendered it, and a draw-over fails naming them. Verify: the script builds

## 2. One announcement per save

- [x] 2.2 An event for a path already gone dates nothing and opens no rebuild. Verify: `AnEventOfAPathAlreadyGone_DoesNotDateTheSaveAfterItsEdit`, in both orders, and `AnEventOfAPathAlreadyGone_AfterTheSavesRebuild_AnnouncesNothing` fail before (mount) and pass after
- [x] 2.1 A rebuild with no save since the last does nothing. Verify: `ASecondCallForTheSaveTheLastRebuildAnswered_AnnouncesNothing` fails before (two announcements, the second a mount) and passes after, with the 29 hot-reload tests

## 3. Proved against the real thing

- [ ] 3.1 The `samples` job's edit-to-view measurement, over several runs. Verify: every save hydrates. Locally, at a load near 50, 30 saves in five rounds: 29 hydrated, and the cold first save of one round drew over with the server's side all saying hydrate (the frame, the reload's flag, the rendered document, the server's line), before the boot said why; with the boot's reason recorded, 18 of 18 hydrated

## 4. Documentation

- [x] 4.1 One entry for the ledger citing #1373, as a file of its own under `docs/ledger/`
