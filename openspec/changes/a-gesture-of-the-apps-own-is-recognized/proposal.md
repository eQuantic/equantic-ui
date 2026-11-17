## Why

Closes #1563, a slice of #194 (pointer, gestures and focus) in the block catalog 1.0 waits for. The previous
slice (#1562) gave any subtree the raw pointer (`Listener`), but a `Listener` only listens: it cannot TAKE a
pointer from a `Pressable`, a `Draggable` or a scroll view, and nothing can. Every gesture an app can have is
one of the nodes the SDK ships (`Pressable`, `Draggable`, `DragDismiss`, `Sortable`, `ContextTarget`'s long
press), and they are resolved by each host in a fixed order of its own. So a gesture the catalog does not ship
(a long press that then drags, a two-step swipe, a press that measures its own pressure of time) cannot be
written in C# at all: an app either gives up or writes JavaScript, which the product principle forbids.
`docs/FLUTTER-PARITY.md` §5 reads `RawGestureDetector, custom recognizers` GAP and `GestureArena` "DIFFERENT,
and thinner: there is no arena".

The general capability: **an app writes its own gesture recognizer in C#, from the raw pointer, and it
competes for the pointer with the SDK's own gestures by one stated rule, on every target.**

## What Changes

- **A recognizer the app writes**: `GestureRecognizer`, an abstract class in the vocabulary that an app
  subclasses in ordinary C#. It is offered each pointer that goes down on its detector
  (`IsPointerAllowed`, `AddAllowedPointer`), hears every later event of the pointers it tracks
  (`HandleEvent`), asks for the pointer with `Resolve(GestureDisposition.Accepted)` or lets it go with
  `Resolve(GestureDisposition.Rejected)`, and is told the outcome (`AcceptGesture`, `RejectGesture`). A
  recognizer that waits for time asks for a deadline (`StartDeadline`) and hears it pass
  (`DidExceedDeadline`), counted by the realizer's own clock. Flutter's names, which are neutral.
- **A block that hosts recognizers**: `RawGestureDetector(child, gestures)`, a layout-transparent wrapper over
  any node, with `Behavior` (the `HitTestBehavior` of #1562) saying where it is hit. `gestures` is a list of
  `GestureRecognizerFactory<T>(create, initialize)`: the recognizer is created once for the detector's
  position and kept across rebuilds, and `initialize` runs on every build to hand it the callbacks the
  latest build wrote, as Flutter's `GestureRecognizerFactoryWithHandlers` does.
- **One arena, written once**: `GestureArena` and `GestureRouter` in the vocabulary, Flutter's
  `GestureArenaManager` and pointer router, written once in C# and transpiled into the runtime, so the web
  and Photon run the same arena. Per pointer: the members in hit order, innermost first; a member that
  accepts wins and the rest are rejected; the last member left wins; at the pointer's up, with nothing
  decided, the first member wins.
- **The SDK's own gestures enter the arena**: a `Pressable`'s press, a `Draggable`, a `DragDismiss`, a
  `Sortable`'s pick-up, a `ContextTarget`'s long press and a scroll view's pan each become a member. A
  built-in that arms (travel past the slop, a hold that lifts) accepts, and the app's recognizers are
  rejected; an app's recognizer that accepts first cancels them, as a scroll's pan already cancels a press.
- **Web**: the browser's own scroll is the one member the runtime cannot hold: it wins by starting a pan,
  which the browser announces with `pointercancel`, and it is kept from starting only while a recognizer
  has already accepted. A press's `click` is swallowed when the arena gave the pointer to another member.
- **Photon**: every member is the host's, so the arena is Flutter's whole: the scroll waits for it like any
  other gesture.
- **Parity**: the `RawGestureDetector` row reads DIFFERENT, with the browser's scroll as the trade; the
  `GestureArena` row is rewritten for the arena that now exists.

For someone using the SDK: a gesture of their own is a C# class and one wrapper around any node, it works the
same in the browser and in the native shells, and it never steals a tap or a scroll it did not win.

## Capabilities

### New Capabilities

- `gesture-recognizers`: an app's own gesture recognizer, written in C#, and the arena in which it competes
  with the SDK's gestures for the same pointer.

### Modified Capabilities

None. The `gestures` and `raw-pointer` requirements hold as they are: a `Listener` still only listens and
never joins the arena.

## Impact

- **Vocabulary** (`eQuantic.UI.Primitives`, contract): `GestureRecognizer`, `GestureRecognizerFactory`,
  `GestureRecognizerFactory<T>`, `GestureDisposition`, `GestureArena`, `IGestureArenaMember`, `GestureRouter`,
  `RawGestureDetector`, one visitor member. Additions only, in `PublicAPI.Unshipped.txt`. Each new type joins
  `primitives-types.fixture.json`, the runtime's exports. The node coverage suites grow by one node.
- **Components** (contract): the `UI.RawGestureDetector` factory.
- **eqc**: no new strategy is expected; an app's subclass of `GestureRecognizer` is a client class like any
  other. Whether every construct the base class uses transpiles is the first check of the tasks.
- **Runtime and the web realizer** (contract): the detector's lowering on both producers; the arena's glue in
  `dom/gesture-arena.ts` beside `dom/pointer-listener.ts`; `dom/pointer-gesture.ts` (Draggable, DragDismiss,
  Sortable), `dom/context-request.ts` and `shared/table-viewports.ts` join the arena.
- **Photon** (experimental): the paint walk records detectors as pointer targets; `PhotonHost` routes
  presses, moves, ups, cancels and frame ticks through the router, and its press, drag, pan, table pan, sort
  and context hold become members. The host's frame-due answer includes the router's next deadline.
- **A pointer event carries its time**: `PointerEvent.TimeMs`, monotonic milliseconds as the platform saw
  the event (the web's `timeStamp`, the Photon shell's event time or the host's clock), so a recognizer
  measures velocity. The record widens: an app that constructs a `PointerEvent` passes the time; one that
  reads it changes nothing. `PointerEvent` has not shipped yet.
- **SDKs, templates, developer surface**: nothing moves. `developer-surface.baseline.txt` is unchanged.
- **Samples**: the dashboard gains a screen whose cards a long press lifts and a drag moves, inside a scroll
  view; the same recognizer's source runs in a Photon host test.
- **Docs**: the wiki's Components page in every language it is written in, the two parity rows and their
  probes in `FlutterParityPinTests`, a ledger entry.

One break, in preview and before `PointerEvent` ever shipped: its constructor takes the time. Migration: `new PointerEvent(phase, position, kind, buttons, modifiers, pointer, scrollDelta)` becomes `new PointerEvent(phase, position, kind, buttons, modifiers, pointer, scrollDelta, timeMs)`. Otherwise nothing an app writes today changes meaning. A `Pressable`, `Draggable` or scroll view with no
detector around it resolves exactly as before, because an arena with one candidate gives it the pointer.

Rules of `docs/PRINCIPLES.md` followed: 1 (a gesture with no line of JavaScript), 4 (the arena is written once
and transpiled, so the two targets cannot disagree on who wins), 5 and 6 (Flutter's names and Flutter's
arena, asked first), 9 (the structural fix: the built-ins become members of one arena rather than one more
special case in each host's fixed order), 15 (a capability is a node around any child). Bent: none; the web's
scroll is a stated trade in the parity row, not a bend.
