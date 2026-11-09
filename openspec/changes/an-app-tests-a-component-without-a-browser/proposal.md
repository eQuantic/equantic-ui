# Proposal

Closes #1245, the first slice (T1) of Feature #294, under the 1.0 epic #898.

## Why

An app author cannot unit-test a component. The repository's own suites can, through fixtures no
consumer can reference, and the shortest path for an author is `WebRealizer.Lower`, which answers
the first frame and nothing after it: it builds every component again on each call, so a stateful
form placed inside a page is drawn at its defaults after every press, which the browser never does.
Nor does anything say what a screen reader announces on the web, where the realizer writes ARIA
inline: fifteen of the repository's own web tests walk the lowered tree by hand to read one
attribute each.

## What Changes

- **A package, `eQuantic.UI.Testing`**, which an ordinary xUnit or NUnit project references. It
  depends on no test framework: it answers values, and the project's own assertions read them.

  ```csharp
  using var tester = new ComponentTester();          // the SDK's theme, the invariant culture, 1280 dp
  tester.Pump(new SignupForm());
  tester.EnterText("Email", "ana@example.com");
  tester.Tap("Sign up");
  tester.Pump();
  tester.Find(SemanticRole.StaticText, "Welcome, ana@example.com");
  ```

  `ComponentTester` takes the theme, the culture, the viewport's width in dp, the density and the
  services a page's prefetch resolves. `Pump(root)` draws the first frame and `Pump()` the next,
  building what a `SetState` marked; `PumpPageAsync(page)` runs every `IServerPrefetch` the tree
  reaches before drawing. `Tap` and `EnterText` act on the control a screen reader announces by that
  name. `Html` is the markup the web realizer lowered, and `Semantics` is what a screen reader
  announces, in reading order, as `Announcement`s that speak `SemanticRole`, never an ARIA string.
  Disposing the tester takes the tree down, so every `OnUnmount` runs.
- **The tester draws frames through the vocabulary's own `BuildOwner`**, a live one like Photon's:
  an instance is kept by its position across frames, `OnMount` runs when the first frame ends, and a
  frame builds only what a `SetState` marked.
- **`WebRealizer.LowerFrame(Element root, …)`** lowers a frame of such a tree: a component is
  lowered from what its position built, by the instance the position keeps, and a layer (an
  overlay's child, an anchored panel) is shown to the owner before it lowers. Its first frame is
  `Lower`'s markup byte for byte, save hydration's unmeasured mark. It is the first step of E4 (#1094)
  and its public form is provisional: E4 decides it.
- **`AdaptiveGates.Shows(gate, widthDp)`** answers whether an adaptive arm's gate shows at a width,
  from the same rule that writes its media query, so the tester's semantics skip the arms the page
  hides at the tester's width.
- **The SDK's own web tests read semantics through the package** where they read the same thing by
  hand.

## Capabilities

### New Capabilities

- `component-testing`: an app's test renders a component headlessly, drives it by name, and reads
  what the web lowers and what a screen reader announces.

### Modified Capabilities

- `element-tree`: a frame of the tree is lowered on the web from its positions.

## Impact

- New project and package `eQuantic.UI.Testing` (`contract` surface), referencing Primitives,
  Components and Web; its API declared in `PublicAPI.Unshipped.txt`.
- `eQuantic.UI.Web`: `WebRealizer.LowerFrame` and `AdaptiveGates.Shows`, declared in
  `PublicAPI.Unshipped.txt`; the internal `ElementFrame` and four sites of the lowering visitor. The
  SSR path is unchanged: every pinned output stays byte-identical.
- `eQuantic.UI.Primitives`: no change. `Elements/` is untouched, as E3a reworks it.
- eqc, the runtime, the Photon shells, the SDKs' targets and the templates: no change. The
  developer surface does not move.
- Docs: a `Testing` page on the wiki in its five languages, `docs/ELEMENT-TREE-PLAN.md` (the test
  host among the owners, and what E4 inherits), and a ledger entry.
- Nothing is removed, so no app needs a migration line.
