# Tasks

## 1. The strings a page reads (#1782)

- [x] 1.1 eqc records the catalogue keys each module reads, and writes `page-strings.json` beside the
  catalogues, following the factory surface only through the factories a module calls
- [x] 1.2 A catalogue holds only what a module the browser reaches reads
- [x] 1.3 The server inlines a document's page and layout strings and every catalogue shipped whole; a
  navigation's payload carries the next page's; a page the index does not name gets the whole catalogue
- [x] 1.4 The browser adds a navigation's strings, and fetches a catalogue whole before it reports a key
  missing
- [x] 1.5 Proved: `APageReadsItsOwnStringsTests` (the index, the factory surface, server-only strings),
  `PageStringsTests` (the document, the layout, the navigation, a page the index does not know), the runtime's
  culture and page-state specs; the dashboard sample's pages carry only their own keys

## 2. One arm in the browser (#1783)

- [x] 2.1 `AdaptiveNode.ResolveIndex`; the element tree holds the one arm at its own slot, and an arm of the
  same type takes the position across a threshold
- [x] 2.2 Photon's layout measures and lays out the arm with the element at its slot
- [x] 2.3 The browser's live owners read whether the document holds every arm; the boot flips it once the page
  has hydrated (or before a page drawn from the start) and renders once; a crossing renders again
- [x] 2.4 The lowering draws the held arm alone and ungated, and keys every arm's box on both web producers
- [x] 2.5 The window's width is kept on the side of each threshold its media query says
- [x] 2.6 Proved: `GlobalKeyTests` and `AdaptiveArmStateTests` (Photon through its host), the runtime's
  `one-arm.spec.ts` (one control, the crossing, the DOM kept by identity after hydration, the zoomed
  window), the component parity fixture regenerated with the keys, and the dashboard in a browser

## 3. The global key (#797)

- [x] 3.1 `GlobalKey`, `VisualNode.GlobalKey`, and the key in `CanUpdate`
- [x] 3.2 Park a keyed position that leaves during the frame's build; retake it, or take it from the parent it
  still stands under where one arm is held; reattach it; settle what no node took
- [x] 3.3 A moved component reads the inherited value above its new place and hears its dependencies changed
- [x] 3.4 A key placed twice fills both places, starts one again, and tells the developer
- [x] 3.5 The native template's `tabs` and `drawer` shells key their screen
- [x] 3.6 Proved: `GlobalKeyTests`, `NativeTemplateShellTests` (the real shells across the threshold, both
  ways), and `ElementEngineTwinTests` executed on .NET and on the transpiled engine in Bun

## 4. Documentation

- [x] 4.1 The wiki's Localization and Adaptive pages, in all five languages, on the PR's wiki branch
- [x] 4.2 `docs/ledger/a-page-carries-only-what-it-shows.md`
