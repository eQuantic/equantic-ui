# Proposal

Closes #1782 and #1783, two sub-issues of the epic #1774 ("Lessons from a real site: what fails only in
the browser is caught by the build and the tests"), and #797.

## Why

A site built on the SDK carried, on every page, what that page never showed:

- **Every string of the app.** The page inlined the whole catalogue, so the shop's pages carried the back
  office's strings, and a test could not say a word was absent from a page (#1782).
- **Every arm of every adaptive node.** Each button of an adaptive screen was in the document two or three
  times: a heavier page, hidden duplicates an automation tool (and the app's own tests) matched, and the
  components of the hidden arms running all the same (#1783).

And the one thing a page should carry across a resize, it lost: a screen built in both arms of an adaptive
node started again when the window crossed the threshold. A phone turning to landscape and back reset the
drawer template's counter to "Pressed 0 times" (#797).

## What Changes

- **A page ships the strings it reads** (#1782). eqc writes, beside the catalogues, which strings each page
  and the layout read through the modules they reach (`page-strings.json`), following the factory surface
  only through the factories a module calls. A catalogue holds only what a module the browser reaches reads,
  so a string only server code reads stays out of it. The server inlines a document's page and layout
  strings, a client navigation's payload brings the next page's, and a catalogue the build ships whole (the
  SDK's own) rides every page. A page the index does not know gets the whole catalogue, and a key the
  browser was not handed is fetched with the rest of its catalogue before it is said to be missing.
- **The browser holds one arm once a page has hydrated** (#1783). A served page still carries every arm,
  each behind its media gate (the hidden ones are `display: none`, out of the accessibility tree), and
  hydrates so. Then the document holds the one arm each window resolves, as Photon does: the other arms
  leave the document and their components unmount, and a window crossing a threshold renders the page again
  with the arm it now shows. The arm stands at its own slot (0 compact, 1 medium, 2 expanded) on every
  target, so a component's server data and its position are named alike on the server, in the browser and
  on Photon. An arm of the same type as the one shown takes its position across a threshold, as Photon
  already kept it.
- **A global key carries a position across parents** (#797). `GlobalKey` is a new vocabulary type, and
  `VisualNode.GlobalKey` a new property. A node that carries one and is placed under another parent in the
  same build takes its position along, with every stateful instance below it, whichever of the two places
  the build reaches first; a key placed twice in one frame fills both places, starts one of them again, and
  tells the developer. The `tabs` and `drawer` shells of `dotnet new equantic-native` key their screen, so
  its state survives the window crossing the threshold.

What an app writes: nothing, for the strings and the arms. To keep a screen's state across an adaptive
node's arms, it keeps a `GlobalKey` in a field of the component that builds the node and hands it to the
screen in each arm: `new ActivityScreen { GlobalKey = _screen }`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `page-culture`: a page ships the strings its modules read.
- `element-tree`: the web holds every arm until the page hydrates, and a global key carries its position.
- `adaptive-layout`: the browser holds the arm its window resolves, and the arm stands at its own slot.

## Impact

- **eqc** (`StringCatalogs`, `PageStrings`, `AppBuild`): the index and the filtered catalogues.
- **The server** (`CultureBridge`, `UIExtensions`): the inlined strings and the navigation payload.
- **The element engine** (`Element`, `BuildOwner`, `NodeElement`, `ComponentElement`, `InheritedElement`,
  `ChildSlots`), written once and transpiled into the runtime.
- **The runtime**: `culture.ts`, `page-state.ts`, the lowering of an adaptive node, a new
  `adaptive-window.ts`, and the boot.
- **The web realizer**: an arm's box carries a key, as the browser's does.
- **Photon**: the layout reads the arm at its own slot.
- **The templates**: the native `tabs` and `drawer` shells.
- **Public surface** (contract, `eQuantic.UI.Primitives`): `GlobalKey`, `VisualNode.GlobalKey`,
  `AdaptiveNode.ResolveIndex` and a setter on `BuildOwner.HoldsEveryArm` are added; nothing is removed. The
  developer surface does not move.

No break: an app that sets no global key behaves as before, except that its page carries one arm and its
own strings.

## Principles

The rules of docs/PRINCIPLES.md this follows: the engine is written once (the browser runs the C# engine,
and the twin cases run both); Flutter is asked first (`GlobalKey` and `_retakeInactiveElement`); the arm a
target holds is named by the same slot on every target, so nothing keyed by a position needs a second
spelling.
