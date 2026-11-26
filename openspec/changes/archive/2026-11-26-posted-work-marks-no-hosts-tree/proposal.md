# Proposal

Closes #1239, a Bug under #1587.

## Why

`LiveRegionAnnouncementTests.ARegionBackToWhatItSaid_OwesNothing` failed once in CI, reading `NeedsRender` true where the region owed nothing. A probe on `PhotonHost`'s outside wakes, run over the whole engine suite, named what sets it. Work posted to the process's UI dispatcher marked every live host's tree as changed, and the suite posts a lot of it from threads with no test of their own:

- `CodeFolding.Ask`'s answer posted from its timer's thread, about 3,000 posts;
- `TreeView.Load` and `OpenAfterPause` calling `SetState` from the pool, about 390.

Each post set `NeedsRender` on every host alive at that moment, the running test's included. An editor whose fold timer outlived its own test told the next test's host that its tree had changed.

It is not only a test's problem. A host that reads `NeedsRender` true at the end of a frame schedules nothing for a live region it owes, since the next frame is coming anyway. And in an app with two windows, a post meant for one tells the other its tree changed.

## What Changes

- **Posted work is the process's.** `PhotonDispatcher.Post` queues the work and marks no host. `PhotonDispatcher.HasWork` says whether work is waiting.
- **A host's `NeedsRender` says what its own tree asked for**, and its `IsFrameDue` says what it scheduled. A post changes neither.
- **The shells ask one question.** `PhotonHost.WantsFrame(now)` is the host's own state, its own schedule, or work waiting for the thread that draws. macOS, Windows, iOS and Android each read it where they read the first two. Work posted from a worker still wakes an idle window, and the frame that runs it builds what the work marks.
- `PhotonDispatcher.WakeOnPost` goes. The dispatcher holds no host.

The native track is experimental (EQX0001). One member is removed there (`WakeOnPost`), and two are added (`HasWork`, `WantsFrame`).

## Impact

- `eQuantic.UI.Native.Components`: `PhotonDispatcher`, `PhotonHost`.
- The four shells' frame loops.
- `UiDispatcherTests` and `LiveRegionAnnouncementTests`.
