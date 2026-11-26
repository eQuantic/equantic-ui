# Tasks

## 1. Measure

- [x] 1.1 A probe on `PhotonHost`'s outside wakes over the whole engine suite names the posters: `CodeFolding.Ask` and `TreeView`'s loads, from threads with no test of their own

## 2. The dispatcher

- [x] 2.1 `Post` queues and marks no host; `HasWork` says whether work is waiting; `WakeOnPost` goes
- [x] 2.2 `PhotonHost.WantsFrame(now)`: the host's own state, its own schedule, or work waiting
- [x] 2.3 macOS, Windows, iOS and Android read `WantsFrame` where they read `NeedsRender` and `IsFrameDue`

## 3. Proof

- [x] 3.1 A worker's post wakes an idle window and leaves its `NeedsRender` false; fails when a post marks every host
- [x] 3.2 A post from another thread while a live region owes leaves it owed and said once; fails when a post marks every host
- [x] 3.3 The engine suite and the testing package pass; the four shells build

## 4. Documentation

- [x] 4.1 One entry under `docs/ledger/`
