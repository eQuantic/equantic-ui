## Context

`docs/FLUTTER-PARITY.md` §5 reads `Listener` PARTIAL (raw pointer only on `Canvas`) and `HitTestBehavior`
PARTIAL (realizer-side, not authorable). The vocabulary already has Flutter's `PointerPhase` and
`PointerKind`, used by the code, sheet and table surfaces. `ContextTarget` (#1531) is the nearest block: a
layout-transparent wrapper over any child, with a document-level controller on the web and regions in
Photon's paint walk.

## How Flutter solves it

`Listener` is a proxy box with `onPointerDown/Move/Up/Cancel/Hover/Signal`, each receiving a `PointerEvent`
with `localPosition`, `kind`, `buttons` and `pointer`. It takes part in hit testing and never in the gesture
arena: it only listens. The pointer router sends every later event of a pointer to the targets its down hit.

`RenderProxyBoxWithHitTestBehavior.hitTest` is the whole of `HitTestBehavior`:

```
hit = hitTestChildren(position) || (behavior == opaque && hitTestSelf)
if (hit || behavior == translucent) result.add(this)
return hit
```

and a stack tests its children topmost first, stopping at the first that returns true. A child "is hit" where
it paints (`RenderParagraph`, `RenderImage`, a decorated box) or where something in it answers (a scrollable
or a detector with `opaque`); layout-only boxes (padding, a column's gap) are not.

## Decisions

### The block and its event

`Listener(child)` with the six callbacks and `Behavior`, a `SingleChildNode`, layout-transparent like
`ContextTarget`. `PointerEvent(Phase, Position, Kind, Buttons, Modifiers, Pointer, ScrollDelta)`; `Position`
is in the listener's own box, so the arithmetic is the app's, as `Canvas` already promises. `PointerButtons`
carries Flutter's constants, which are also the DOM's `buttons` bits, so the web reads them as they come.
`PointerPhase` gains `Scroll` (append-only: `EnumValueAbiTests`). Hover is a mouse or a pen moving with nothing
pressed; a finger never hovers, as `PointerKind` already states. The leave of a hover is `Hoverable`'s, as
Flutter's is `MouseRegion`'s.

### One hit rule, written twice and pinned once

The rule is Flutter's, stated over what both targets can see at a point: the entries under it in paint order,
each one a LISTENER (with its behaviour and its subtree) or a SOLID (something that draws or answers). For a
listener L:

- its child is hit when a solid inside L is under the point, or an `Opaque` listener inside L is;
- L is reached when nothing painted above it, outside its own subtree, is a solid or a listener that returns
  true (Opaque, or with its child hit);
- L hears when it is reached and its child is hit, or its behaviour is not `DeferToChild`;
- the listeners that hear are told innermost and topmost first, Flutter's dispatch order.

The rule is pure: `ListenerHits` in C# (Photon) and `listenerHits` in TypeScript (web), each over its target's
entries, and `listener-hits.fixture.json` holds the cases both suites run, so the two writings cannot drift.

### What a solid is, per target

- **Web**: the elements `elementsFromPoint` returns, which are the browser's own hit test. An element is a
  solid when it draws (a fill, a background image, a visible border, a shadow, its own text, an image, a
  vector, a canvas, a video) or answers (a button, a link, a field, an editable, a focusable element, a scroll
  container). A listener is `[data-eq-listener]`.
- **Photon**: from the first listener on, the paint walk records an entry per node it draws, its box on
  screen through the clips and transforms the input regions already go through. An entry is a solid when the
  node painted something of its own (commands written outside its children's) or registered input (a press,
  a drag, a scroll, a text, a canvas). A frame with no listener records nothing.

A fill that paints nothing draws nothing on both targets: the web cannot tell an explicit transparent fill
from none. Flutter's idiom of a transparent `Container` to catch a tap in empty space is `Opaque` here, which
is what the behaviour is for.

### What a listener does to everything else

Nothing. A listener reports and takes nothing: the web adds passive listeners and prevents no default, and
Photon's dispatch runs beside its press, drag and scroll routing, not instead of it. So `Opaque` hides the
listeners behind it, never a button: what a box drawn over a button does to its press stays each target's
existing rule (`hit-targets`). This is the one place the shape differs from Flutter's, where an opaque
detector also hides the detectors behind it, because the web delivers a press to one element and a listener
must not take it.

### A pointer is followed

The listeners that heard a down hear that pointer's moves, its up and its cancel, wherever it goes, each with
the position in its own box (negative or past its size outside it), until the up or the cancel. The web keys
them by `pointerId`; Photon has one pointer, as its shells do.

### Buttons on Photon

The shells deliver presses as the primary button and a secondary click as a context request. The host's
`ContextRequest` takes the button the shell saw (the secondary by default), and a listener hears a secondary
click as its down and its up at the request: the macOS shell passes the primary for a Control-click, which the
web reports as a primary press with Control held. No other shell changes.

### Scroll

`OnPointerScroll` hears a wheel or a trackpad over the listener, `ScrollDelta` in dp toward the content's end
(the DOM's `deltaX/deltaY`, lines and pages turned into dp; Photon's `ScrollBy(across, down)`). A scroll view
inside still scrolls.

## Risks / Trade-offs

- The web's solid test reads computed style per element under the pointer, for each event, and only while a
  listener exists in the document. The elements under a point are few.
- Photon records one entry per drawn node after the first listener, for frames that have one. A frame without
  a listener records nothing, so the steady-frame allocation ceiling is untouched.
