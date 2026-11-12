# Tasks

## 1. The boundary is a tree's own

- [x] 1.1 `ComponentBoundary` becomes an instance: its diagnostics, report and tally, and the builds through it. The three `AsyncLocal`s go. Verify: `AmbientChannelsTests` passes with their entries gone
- [x] 1.2 The web's measurer, `MarkupHost`, `RenderChannels` and `WebRealizer.Lower` build through the tree's boundary. Verify: the web `ComponentBoundaryTests` report and count through a boundary they hold

## 2. Each host keeps and arms its tree's

- [x] 2.1 The server makes one per request and hands it to its host and each round's channels. Verify: the server suite and the SSR fixtures, unchanged
- [x] 2.2 `PhotonHost.Boundary`, armed by every shell through `PhotonApplication.ArmBoundary` and read in its frame summary, and by the design host. Verify: `AComponentContainedInOneWindow_IsCountedByThatWindowAlone`, and `ContainedIsNotSilentTests` asserting what they asserted
- [x] 2.3 The suites. Verify: web, server, component testing, native engine and compiler pass

## 3. Docs

- [x] 3.1 Verify: `docs/ELEMENT-TREE-PLAN.md` records E4c, the wiki's WriteOnceComponents and Capabilities pages in their five languages and Upgrading in its two say the boundary is the tree's, and a ledger entry cites #1452
