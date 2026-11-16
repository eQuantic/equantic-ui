- **2026-11-12 · A tree's error boundary is its own**: `ComponentBoundary`'s diagnostics, report and
  tally were three `AsyncLocal`s, so two windows in one process shared one tally and a window that drew
  a healthy screen was accused of what another one contained. The boundary is an instance now, one per
  tree, and the tree's owner is made from it. The server keeps one for a request and hands it to the
  request's host and each round's channels, a Photon host keeps its window's (`PhotonHost.Boundary`),
  and every shell arms it with the app's report (`PhotonApplication.ArmBoundary`) and reads its tally
  beside the frame summary. The SSR fixtures are unchanged
  ([#1452](https://github.com/eQuantic/equantic-ui/issues/1452), under
  [#1094](https://github.com/eQuantic/equantic-ui/issues/1094)). Proposed and archived through
  OpenSpec (`openspec/specs/element-tree`).
