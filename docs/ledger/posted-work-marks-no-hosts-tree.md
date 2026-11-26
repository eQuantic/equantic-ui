- **2026-11-26 · Work posted to the thread that draws is the process's, and marks no host's tree**: a live region's
  test read `NeedsRender` true where the region owed nothing, now and then. A probe on the host's outside wakes, over
  the whole engine suite, named why. Every post to Photon's UI dispatcher marked every live host as changed, and an
  editor's fold answer posted from its timer's thread (about 3,000 posts in a run) or a tree view's load (about 390)
  told whichever test was running that its own tree had changed. In an app, the same post told every other window so,
  and a host that read itself changed scheduled nothing for a live region it owed. A post now queues and marks no
  host. The shells ask `PhotonHost.WantsFrame`: the host's own state, its own schedule, or work waiting for the thread
  that draws. `PhotonDispatcher.WakeOnPost` is gone ([#1239](https://github.com/eQuantic/equantic-ui/issues/1239)).
  Proposed and archived through OpenSpec (`openspec/specs/native-ui-dispatcher`).
