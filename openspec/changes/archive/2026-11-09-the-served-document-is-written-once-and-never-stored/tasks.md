# Tasks

## 1. Each value written once

- [x] 1.1 Fill every placeholder of the template in one pass. Verify: `ShellWritesAValueOnceTests`, three cases, fail with the engine as it was and pass
- [x] 1.2 Read a served page whose server data holds a placeholder, through `MapUI`. Verify: the field keeps the text and the state stays in its script

## 2. Never stored

- [x] 2.1 Send every answer of a page's route `no-store`, said once in `ServeAppShell`. Verify: `TheShellIsNeverStoredTests` fails for the three documents before and passes after, and the navigation's state stays `no-store`

## 3. Proved against the real thing

- [x] 3.1 Run the Server suite whole, the wiki guards, and the Conformance suite, which serves pages through the shell (`HydrationCrossingTests`). Verify: Server 469 of 469 and the wiki guards 72 of 72 on the branch with main merged, and Conformance on CI's two `test` jobs

## 4. Documentation

- [x] 4.1 The wiki's Security page in its five languages, on a branch named like the pull request's
- [x] 4.2 One entry for the ledger citing #1319 and #1288, as a file of its own under `docs/ledger/`
