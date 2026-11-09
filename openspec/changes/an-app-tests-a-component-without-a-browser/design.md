# Design

## How Flutter solves it

`flutter_test`'s `WidgetTester` mounts a widget into the REAL element tree under a test binding:
`pumpWidget` builds and lays out the first frame, `pump` draws the next one (what `setState`
marked), `tap` and `enterText` drive the control a finder locates, and `getSemantics` with
`matchesSemantics` reads what the platform's screen reader would get. Nothing is mocked below the
widget: the elements, the states and the build phase are the app's own.

The same pieces exist here (docs/FLUTTER-PARITY.md, the `Element`/`BuildOwner` and `setState` rows):

| Flutter | Here |
|---|---|
| `WidgetTester` | `ComponentTester` |
| `pumpWidget(widget)` / `pump()` | `Pump(root)` / `Pump()` |
| `tap(finder)`, `enterText(finder, text)` | `Tap(name)`, `EnterText(name, text)`: the finder is the name a screen reader announces |
| `BuildOwner` under the test binding | `BuildOwner`, live, as Photon's |
| `getSemantics` → `SemanticsData` | `Semantics` → `Announcement`, in `SemanticRole` |
| `binding.hasScheduledFrame` | `HasScheduledFrame` |

What Flutter does not have is the server's prefetch, which `PumpPageAsync` runs before the first
frame.

## Decisions

### A live owner, and a frame lowered from its positions

A test draws more than one frame, so it needs what a host that draws more than once has: an owner
that keeps a stateful instance at its position, runs `OnMount` and `OnUnmount`, and builds only what
a `SetState` marked. The vocabulary already has it, `BuildOwner`, and Photon draws through it.

The web realizer's `Lower` builds every component of the tree it is handed. That is the server's one
frame; over a second one it draws a stateful component that a parent builds at its defaults, because
the parent's build makes a fresh instance and the state lives on the one the element kept.
`WebRealizer.LowerFrame` lowers a frame of the owner's tree instead: a component is lowered from what
its position built, and a layer is shown to the owner before it lowers, so what the layer holds
builds in the slot that keeps it. That is the realizer half of E4 (#1094, "the server renders
through an owner per request"), delivered first because the tester needs it; the server does not
move onto it here.

The frame is a field of the lowering visitor, which is one instance per lowering, rather than a new
`AsyncLocal`: section 3.5 of docs/ELEMENT-TREE-PLAN.md moves the web's channels off the async flow
and onto the owner, and a seventh channel would be a step the other way.

The frame finds every position by walking the tree with `Element.ChildAt`, the only enumeration the
engine exposes. `NodeSlots` states the invariant that makes the walk exact: a node's children are a
list, which holds no empty slot, or at most two fixed slots, either of which may be empty. So only
the first slot can be empty with a child after it. The walk adds nothing to `Primitives/Elements`,
which E3a is reworking.

A component the frame has no position for, and a layer whose holder it has none for, is refused with
an exception rather than built fresh: built fresh it draws its defaults and nothing says so. Photon
refuses a layer with no position for the same reason.

### What a frame lowers that the tree does not hold

An adaptive node's element holds the one arm its size class resolves, as Photon draws one; the web
lowers every arm behind a media gate, and the browser shows one. In a frame, the held arm lowers from
its positions and the others lower as a tree drawn once does. They are the arms the page hides at
that size, and a change of size replaces the arm's subtree on both targets, so no instance they would
have kept is ever shown.

### Byte-identical, save one mark

The first frame of every case of the component parity fixture, at every size class, is `Lower`'s
markup compared whole. One attribute is the server's alone: `data-eq-unmeasured`, which tells
hydration that a component built on widths nobody measured. The lowering counts what each
component's build asked the measurer, and in a frame the owner built every component, so the count
is the owner's to keep. A tester's frame is never hydrated; the request owner of E4 is, and
attributes it there.

### What a screen reader announces, read from the ARIA the web writes

On the web the realizer writes semantics inline as ARIA, and the platform's accessibility tree is
computed from it. The tester computes the same thing, for the markup this realizer writes: the role
from the `role` attribute or the element's own (a `button`, an `a` with `href`, an `input` by its
`type`, an `img` with a non-empty `alt`, `h1` to `h6`), the name from `aria-labelledby`, then
`aria-label`, then `alt`, then the text a name-from-content role holds, and the states from
`disabled`, `aria-disabled`, `aria-checked`, `aria-selected`, `aria-expanded`, `aria-current`,
`aria-valuetext`, `aria-valuenow` and an input's `value`. What is hidden is skipped: an
`aria-hidden="true"` or `hidden` subtree, an inline `display: none` or `visibility: hidden`, and an
adaptive arm whose gate does not show at the tester's width.

An `Announcement` speaks `SemanticRole`, so an author's test never names an ARIA role, and the native
track's `SemanticNode` can be projected onto the same shape when #339 compares the two.

The tester lowers WITHOUT a style sink, so a declaration stays inline where the reader can see it;
the server writes the same declarations as atomic classes.

Whether an adaptive arm shows is the gate's own question, answered by `AdaptiveGates.Shows` beside
the media query it writes, rather than by a second parser of gate names in the package.

### Typing reaches the vocabulary, not the markup

The server's lowering attaches no input handler (the runtime does, at hydration), so `EnterText`
finds the `TextEntry` the tree holds whose label is the name and calls its `OnChanged`, which is what
the runtime's input handler calls. `Tap` invokes the click handler the lowered markup carries, which
is the `Pressable`'s own.

### Defaults

The SDK's theme (`PhotonTheme`, what `AddUI` serves without `UseTheme`), the invariant culture so a
test reads the same on every machine, a 1280 dp viewport, and comfortable density. The package
depends on no test framework.

## Divergences from docs/ELEMENT-TREE-PLAN.md

Both are written into the plan in this change.

- **A fourth kind of host.** Section 3.6 lists one owner per Photon window, per server request, per
  browser document and per email message. A component test is a host too, with a LIVE owner like
  Photon's, because a test drives frames and input as a browser does. The server stays structural,
  as 3.7 sets.
- **A prefetch rebuilds every component, not its subtree.** Section 3.7 has a completed prefetch mark
  its element and rebuild that subtree. Marking is internal to the engine, and this change does not
  touch `Primitives/Elements`, so `PumpPageAsync` rebuilds every component after a round: the same
  instances, the same answer, more builds. E4 replaces it with the mark.

## Not in this change

- What Photon lays out (T2, #1246), the variant matrix (T3, #1247) and the parity of the two
  semantics (#339).
- Work an `OnMount` starts and finishes after an `await`: its `SetState` runs on the thread the
  continuation resumes on. A page's server data arrives through the prefetch, which the tester runs.
- Attributing hydration's unmeasured mark per component from the owner's measurer, which E4 needs
  and a tester does not.
