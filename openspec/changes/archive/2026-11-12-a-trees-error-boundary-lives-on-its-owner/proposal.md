# Proposal

Closes #1452, E4c, under #1094 (E4) and #937, the element tree. Split from #1404 (E4b), which handed a render its web channels.

## Why

`ComponentBoundary`'s diagnostics, report and tally were three `AsyncLocal`s. Two windows in one process shared one tally, so a window that drew a healthy screen was accused of what another one contained, and a shell that refuses to exit zero while the tally is non-empty failed for a window it never drew. Any code in the flow could read or overwrite them, and the server cleared the report after each round by hand. They belong to one tree, not to an async flow (docs/ELEMENT-TREE-PLAN.md 3.6).

## What Changes

- **`ComponentBoundary` is an instance, one per tree.** `Diagnostics`, `Report`, `Contained` and `ClearContained()` are its own, and so are the builds through it: `Owner()`, `StructuralOwner()`, `BuildAtDepth`, `BuildContained` and `ExpandContained`. The tree's owner is made from it. The expansion walk keeps its thread-static depth, and the email's walk keeps `Enter`, which has no boundary.
- **The server keeps one for a request**, with its logger and the environment's diagnostics, and hands it to the request's `MarkupHost` and to each round's `RenderChannels`, so an escape-hatch page's write-once bridges report with the page. A discovery keeps a silent one, as it reported nothing before.
- **A Photon host keeps its window's** (`PhotonHost.Boundary`, or one handed to its constructor). Every shell arms it with the app's report (`PhotonApplication.ArmBoundary`) and reads its tally beside the frame summary. The design host arms its own, and a tree laid out once builds through a boundary of its own.
- **What an app and a test reach.** `PhotonApplication.ContainedReport`, set before `Run`, is the report every window is armed with in place of the app's logger, the seam the static report was. `ComponentTester.Boundary` and `PhotonTester.Boundary` name what a test's tree contained.
- **`AmbientChannelsTests` loses the boundary's three entries**, so one that comes back fails it.

The SSR fixtures and the hydration payload do not change. An app's pages and components change nothing, and every Photon window still reports to the app's logger. The public surface moves where a host meets the boundary: the static members go and become the instance's, `PhotonHost`, `MarkupHost` and `RenderChannels` take a boundary, `PhotonApplication.ArmBoundary` arrives. No developer surface moves. The migration line: `ComponentBoundary.Contained` becomes `host.Boundary.Contained`, and `component.BuildContained(context)` becomes `boundary.BuildContained(component, context)`.

## Impact

The vocabulary's boundary, the web realizer, the server's rendering service, the Photon host, the four shells, the Photon design host, and the tests that read the tally. The proof is a test of two windows whose tallies and reports stay apart, the contained-is-not-silent tests unchanged in what they assert, and the SSR fixtures byte for byte.
