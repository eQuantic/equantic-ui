- **2026-11-27 · A release commit keeps the parent its runtime budget is measured from**: a Bug under #1587
  ([#1930](https://github.com/eQuantic/equantic-ui/issues/1930)). The `v0.2.0-preview.73` tag's run and main's run
  on the same commit failed, and nothing was published. The served runtime's budget (#1922) measures a change
  against the checkout's first parent, which CI checks out two commits deep. The jobs then fetched the release
  tags one deep, and on a shallow clone a fetch sets each tip's history to its depth. A release commit is its own
  tag's tip, so that fetch cut it to a root, where every commit no tag points at kept its parent. Both tag fetches
  are now two deep. `ReleaseCheckoutTests` rebuilds that checkout, runs every tag fetch it reads in `ci.yml` and
  refuses one that loses the parent: at one deep it failed, naming the command. The same runs found a wiki mark
  that dated `LayoutBuilder` to 0.2.0-preview.69, the version in the tree when #1555 was written, which the
  version-mark guard could tell only once the tag existed. It now says 0.2.0-preview.73 in every language.
