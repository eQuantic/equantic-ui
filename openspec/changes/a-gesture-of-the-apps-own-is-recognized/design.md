## Context

`docs/FLUTTER-PARITY.md` §5 has three rows this change reaches:

- `GestureArena, competing recognizers`: **DIFFERENT, and thinner.** "There is no arena: press, drag and
  scroll are resolved by the host in a fixed order."
- `GestureDetector`: **DIFFERENT, and better for this SDK**: a node per intent (`Pressable`, `Draggable`,
  `DragDismiss`, `Hoverable`, `Adjustable`, `Navigable`). This row stays as it is.
- `RawGestureDetector, custom recognizers`: **GAP.**

The previous slice (#1562, `openspec/specs/raw-pointer`) gave any subtree the raw pointer (`Listener`,
`PointerEvent`, `PointerButtons`, `HitTestBehavior`) and one hit rule written twice and pinned by one fixture
(`ListenerHits.cs`, `listener-hits.ts`, `listener-hits.fixture.json`). A listener only listens. This change is
what TAKES a pointer.

### How each target resolves a pointer today (read, not recalled)

**Photon** (`PhotonHost.PressDown` / `PointerMove` / `PressUp`, "Gestures v2"). One pointer
(`TheOnePointer = 1`). At the press the host arms candidates in a fixed order, each one skipped when an
earlier one is armed: a canvas owns the pointer and returns at once; a table viewport's scroll (its bar is
the scroll's at once, its cells a candidate pan); the topmost drag surface (`Draggable`, `DragDismiss`); a
sortable item, only without a drag surface (#1537); a finger's context hold (#1531); the topmost scroll
view's pan, only without a drag surface; then the press itself (the topmost enabled `Pressable`), or a field,
a code surface, a sheet. On a move, past the slop along the gesture's own axis (`Touch.PressCancelSlop =
12`, in the gesture's space, `SlopIn`) a drag or a pan ACTIVATES and clears `_pressed`; the table pan the
same. A finger's sort lifts after `SortPlacement.HoldMs = 250`, a mouse's after `SortPlacement.PickUpTravel =
5`; a context hold asks after `ContextTarget.HoldMs = 500`, counted by the host's frame clock
(`TickContext`, `ContextDueMs`) and dropped by travel past the slop, and a hold that asked leaves the press
swallowed (`_pressSwallowed`), so its release presses nothing. Nothing in it can be asked "may I have this
pointer?": the order is the rule. A mouse's drag pans a scroll view on Photon (no kind gate on `_pan`).

**Web.** Every gesture is its own document-level controller, and none knows the others:

- a `Pressable` is a native element whose `click` the browser fires after the up;
- `Draggable`, `DragDismiss` and `Sortable` share `dom/pointer-gesture.ts` (`followPointer`, `SLOP = 12`):
  each arms on its own (travel past the slop, or a finger's hold), prevents a non-passive `touchmove` only
  once armed, and swallows the `click` its release ends in;
- the context hold is `dom/context-request.ts` (`HOLD_MS = 500`, `SLOP = 12`), its own `setTimeout`;
- a table viewport pans itself (`touch-action: none`, `table-viewports.ts`, `PRESS_SLOP = 12`);
- a scroll view is the browser's own: with the default `touch-action`, the browser decides to pan by its own
  slop, before any script can be asked, and announces the decision with `pointercancel` (Pointer Events,
  "the user agent MUST fire pointercancel when it starts a pan"); a mouse never pans.

So both targets agree on today's outcomes for the built-ins (slop 12, hold 500, lift 250, cross-pinned
constants), and neither has a place where a gesture the SDK does not ship could take part.

## How Flutter solves it

Three trees, and the question lives in the THIRD (the render tree's hit test) and in a binding beside it:

1. A pointer down is hit-tested; the result is the path of render objects under it, innermost first.
2. `GestureBinding` dispatches the down along the path. Each `RawGestureDetector` on it hands the down to its
   recognizers (`addPointer` → `isPointerAllowed` → `addAllowedPointer`), and each recognizer that wants the
   pointer registers a route for its later events (`PointerRouter`) and joins the pointer's arena
   (`GestureArenaManager.add`), in dispatch order, so the innermost member is the first.
3. After the down's dispatch the arena CLOSES: no member joins later. A lone member wins at once.
4. Members resolve: `resolve(accepted)` makes it the winner at once if the arena is closed (an "eager winner"
   while it is still open, settled at the close); `resolve(rejected)` removes it, and when one member is left
   it wins. Winners hear `acceptGesture(pointer)`, everyone else `rejectGesture(pointer)`.
5. At the up the arena is SWEPT: with nothing decided and no HOLD, the FIRST member wins and the rest lose.
   A member may `hold` the arena past the up (a double tap) and `release` it later.
6. The SDK's own gestures are recognizers like an app's: a tap waits for the sweep (it never accepts
   early), a drag (and so a scroll) accepts past `kTouchSlop`, a long press accepts at `kLongPressTimeout`
   (500 ms, the same as `ContextTarget.HoldMs`).
7. `RawGestureDetector(gestures: Map<Type, GestureRecognizerFactory>)`: a widget is rebuilt constantly, so the
   detector's STATE (the second tree) keeps one recognizer per type, created by the factory's constructor once
   and handed fresh callbacks by its initializer on every rebuild (`GestureRecognizerFactoryWithHandlers`), and
   disposed with the state.

## Goals / Non-Goals

**Goals:** an app writes a recognizer in C#; one wrapper hosts it over any node; one arena, written once, in
which the recognizer and the SDK's press, drag, sort, context hold and scroll compete by Flutter's rule;
the rule stated honestly per target; a sample's long press that then drags, beside a scroll view, on both.

**Non-Goals:** the focus object (#1565); built-in recognizer classes for an app to reuse (`TapGesture…`,
`PanGesture…`): the nodes per intent stay the SDK's answer (`GestureDetector` row); an arena HOLD (see Open
Questions); several simultaneous pointers on Photon, whose shells report one; replacing the built-ins' own
arming logic, which stays as it is and only reports to the arena.

## Decisions

### 1. The recognizer is a class the app subclasses, with Flutter's names

`GestureRecognizer`, abstract, in `eQuantic.UI.Primitives/Gestures`, implementing `IGestureArenaMember`:

| member | who calls it | what it is |
|---|---|---|
| `virtual bool IsPointerAllowed(PointerEvent down)` | router | whether it wants this pointer (a button, a kind); default yes |
| `protected virtual void AddAllowedPointer(PointerEvent down)` | router | it joined the arena for the pointer and tracks it; override to read the down |
| `protected abstract void HandleEvent(PointerEvent ev)` | router | every move, up and cancel of a pointer it tracks, in its detector's box |
| `protected void Resolve(GestureDisposition)` / `ResolvePointer(int, GestureDisposition)` | the recognizer | asks for, or lets go of, every pointer it tracks, or one |
| `virtual void AcceptGesture(int pointer)` / `RejectGesture(int pointer)` | arena | the outcome |
| `protected void StopTrackingPointer(int pointer)` | the recognizer | hears that pointer no more |
| `protected void StartDeadline(float milliseconds)` / `virtual void DidExceedDeadline(int pointer)` | the recognizer / router | a time it waits for, counted by the realizer's clock |

Every name is Flutter's and none would be missing if the web did not exist ("would this word exist if the
web did not?": recognizer, arena, gesture, deadline all would). `GestureDisposition` is Flutter's enum,
append-only like every vocabulary enum. Why a class to subclass rather than a record of delegates: a
recognizer is a small state machine with private state per pointer, the shape C# writes as a class, and an
app's subclass is client code eqc already transpiles (an `InheritedComponent` subclass is the precedent of an
app class deriving from a vocabulary class).

A recognizer OWNS NOTHING a realizer must release: its deadline is the router's, which the realizer counts.
So there is no `Dispose` to remember, unlike Flutter's, whose recognizers hold their own `Timer`. The cost:
a recognizer cannot start an arbitrary timer of its own; a deadline per pointer is what Flutter's
`PrimaryPointerGestureRecognizer` needs, and it covers the long press.

### 2. The detector hosts factories, and the recognizer outlives a rebuild

`RawGestureDetector(VisualNode child, IReadOnlyList<GestureRecognizerFactory> gestures)` with
`Behavior : HitTestBehavior` (default `DeferToChild`), a layout-transparent `SingleChildNode` like `Listener`.
A factory is `new GestureRecognizerFactoryWithHandlers<T>(Func<T> create, Action<T> initialize) where T :
GestureRecognizer`, over a non-generic `GestureRecognizerFactory` the list holds. Both are Flutter's names. The
generic one is not `GestureRecognizerFactory<T>` beside its base: JavaScript has no arity, so the two would be
two twins of one name, which eqc refuses in an app (EQ1005), and which collided in the shared library's
transpilation when this change first wrote them so.

Measured with a single-file `dotnet run` program on .NET 10.0.301 (the two classes as written here, not
committed): a C# class generic is invariant, so `GestureRecognizerFactoryWithHandlers<HoldThenDrag>` is NOT a
`GestureRecognizerFactoryWithHandlers<GestureRecognizer>` (`is` answered `False`), which is why the list holds
the non-generic base; and an app writes
`new GestureRecognizerFactoryWithHandlers<HoldThenDrag>(() => new(), r => r.OnMoved = Move)`: the target-typed
`new()` inside the lambda compiles and creates a `HoldThenDrag`.

Why factories and not instances (the alternative, Flutter's `TextSpan.recognizer` shape, where a
`StatefulComponent` creates the recognizer in a field and hands the instance to the node): a `VisualNode` is
rebuilt constantly, and on Photon every frame (the first tree, which must not hold state), so an instance
created in `Build` would be a new one each time. Instances work only in a stateful component, and the
catalog's components are mostly stateless; the issue asks that an app use the block "exactly as the catalog
does". Factories cost a retained store per position on each realizer, which both already have for other
gestures (Photon keys `_drags` and scroll offsets by the position's `Element`; the web keys state by the DOM
element the reconciler keeps).

Where the recognizers live, per target (the SECOND tree's job, as in Flutter):

- **Photon**: the host keeps each detector's recognizers by its `Element` (the position), runs `initialize`
  at every walk that meets the detector, and drops them when the position leaves the frame.
- **Web**: `dom/gesture-arena.ts` keeps them in a `WeakMap` keyed by the detector's DOM element (the position
  the reconciler keeps). The lowering's handler, which the reconciler replaces on every render, answers the
  controller's request with the latest factories, so `initialize` runs with the callbacks the latest build
  wrote before the recognizers hear a down.

Recognizers are kept by their PLACE in the list, as unkeyed children are. A place keeps its recognizer while
the factory handed to it is of the same type as the one before (`factory.GetType() != maker.GetType()` makes a
new one), which is Flutter's `canUpdate`, asked of the factory as the element engine asks it of a node (#1202).

Measured by task 1.5: the first shape, `recognizer is T` inside the factory, transpiled to `recognizer != null`,
true for every recognizer, and nothing said so (filed as #1846). The runtime-type comparison is a construct eqc
translates and its Conformance suite runs. One gap is left, on the web only. A generic class declared outside
the app is not marked with its type arguments when the app constructs it (`ObjectCreationStrategy.ClosureOf`
marks only a type the source declares). So until eqc marks it, `GestureRecognizerFactoryWithHandlers<A>` and
`<B>` compare as one type in the browser, and a place handed a factory of another recognizer type keeps the one
it has there. Photon answers it as .NET does. The gap is eqc's, on #1846, and the change's spec scenario for it
is proved on Photon and waits on the web.

### 3. One arena and one router, written once in C# and transpiled

`GestureArena` (Flutter's `GestureArenaManager`) and `GestureRouter` (Flutter's `PointerRouter` and the
binding's dispatch; the name #194 reserved for the PhotonHost split's gesture half) live in Primitives and
cross into the runtime as the element engine and `SortPlacement` do. The web runs the transpiled twin, Photon
runs the C#: the two targets cannot disagree on who wins, and there is no second writing to pin. The arena's
cases are still written as data (`gesture-arena.fixture.json`) and run on both sides, because the twin is a
translation and the conformance rule is that both sides execute.

The arena, per pointer: `Add(pointer, member)` returns the entry the member resolves with; `Close`; `Sweep`.
The rule is Flutter's items 3 to 5 without the hold. Member order is hit order, INNERMOST FIRST, ties
topmost first: the order `ListenerHits` already gives listeners, extended to the built-ins by containment
(Photon's `Encloses`, the DOM's `contains`).

The router, per host: `Down(pointer, members, down)` adds the members in order, offers each recognizer the
pointer (`IsPointerAllowed`, `AddAllowedPointer`), and closes the arena; `Route` hands a move, an up or a
cancel to the recognizers tracking the pointer, each in its detector's box (the realizer localizes, as it
does for a listener); an up sweeps; a cancel rejects every member; `Tick(nowMs)` fires the deadlines due
and `NextDeadlineMs` says when the next one is.

### 4. The SDK's gestures become members, keeping their own arming

Each built-in keeps the arming it has, and reports it:

| built-in | joins when | accepts when | rejected, it |
|---|---|---|---|
| press (`Pressable`) | the innermost enabled one under the down | never early: it wins at the sweep, when it is the first member | presses nothing on release (Photon: `_pressSwallowed`; web: its `click` swallowed) |
| `Draggable`, `DragDismiss` | armed as a candidate | it arms (travel past the slop on its axis) | drops its candidate (Photon `_drag`; web detaches `followPointer`) |
| `Sortable` pick-up | armed as a candidate | it lifts (hold or pick-up travel) | drops it (`CancelSort`; web detaches) |
| context hold | a finger's hold armed | its menu is asked | stops counting |
| scroll view pan | armed as a candidate | Photon: past the slop; web: the browser's `pointercancel` | Photon: drops `_pan`; web: see 5 |
| table viewport pan | armed as a candidate | past its slop | cancels the viewport's pointer |

Built-ins among themselves resolve exactly as today, by construction: the host arms the same candidates in
the same order, and the first to arm accepts, which rejects the others, which is what clearing `_pressed`
did. A built-in alone in its arena wins at the close, which is today's behaviour, so an app with no detector
sees nothing change. The existing suites (`SortableHostTests`, `ContextTargetHostTests`, the drag and scroll
host tests, `draggable.spec.ts`, `sortable.spec.ts`, `context-request.spec.ts`) are the proof and must stay
green unchanged.

A field, a code surface and a sheet surface are NOT members in this slice: they take the press and its drag
selection at the down, with no slop, and none has a cancel on the web (a native field's selection cannot be
taken back). A recognizer around one hears the pointer and may accept; the surface keeps what it took. Stated
in the spec as a non-goal and in the parity row.

A `Canvas` keeps its pointer as it does today; the arena still opens before the host's canvas branch returns,
so a detector around a canvas competes with the press, drag and scroll around it.

### 5. The arena rule per target, honestly

**Photon: Flutter's arena, whole.** Every member is the host's, so a scroll view's pan WAITS: it is a member
that accepts past the slop, and a recognizer that accepted first (a hold) has rejected it, so the list does
not move under a drag that began with a hold. A recognizer that accepts takes the pointer from every built-in
under it; a built-in that arms first takes it from the recognizer.

**Web: Flutter's arena for everything the runtime runs, and the browser's scroll as a member it cannot hold.**

- The press: the runtime cannot stop the browser firing `click` after the up, so a press that lost has its
  `click` swallowed in the capture phase (as `followPointer` and the context hold already do). Its `:active`
  look during the press is the browser's and is not taken back. Same outcome as Photon.
- Draggable, DragDismiss, Sortable, the context hold, the table pan: the runtime's own; same as Photon.
- The browser's scroll: it does not wait for the arena. It decides to pan by its own slop and `touch-action`,
  and says so with `pointercancel`, which the arena reads as the scroll's acceptance (every member is
  rejected, as a cancel rejects them in Flutter). The runtime keeps it from panning only by preventing a
  `touchmove` while it is still cancelable, which the arena does once a recognizer has WON (a non-passive
  `touchmove` on the document, `followPointer`'s mechanism). So on the web a recognizer takes the pointer
  from a scroll only if it accepts before the browser pans: a hold (the finger still, so no pan) always can;
  a recognizer that accepts on travel along the scroll's axis races the browser's slop.
- A mouse never pans on the web; once a recognizer won, `selectstart` and `contextmenu` are prevented so the
  drag that follows selects no text and opens no menu.

The arena's down opens in a capture-phase `pointerdown` on the document (before every controller's bubble
delegate, so the built-ins join an open arena) and closes in a bubble-phase `pointerdown` on the `window`,
which runs after every document listener. Not a microtask: the HTML spec runs a microtask checkpoint after
each listener of a user-initiated event, so one queued at the capture would close the arena before the
built-ins joined (from the spec, not measured here).

**Verdict: the `RawGestureDetector` row reads DIFFERENT**, with the trade named: on Photon the arena is
Flutter's, scroll included; on the web the browser's scroll is a member that wins by panning and that the
arena can only forestall, never overrule, and the click of a lost press is swallowed rather than never fired.
The `GestureArena` row is rewritten from "there is no arena" to the arena as it now is (written once, the
built-ins as members, no hold), and its probe from `Nothing("GestureArena", "GestureRecognizer")` to `Has`.

### 6. Deadlines on each realizer's clock

`StartDeadline(ms)` registers with the router. Photon ticks the router before a frame with the frame's time
(as `TickContext` does) and folds `NextDeadlineMs` into `IsFrameDue`, so a shell that is idle still wakes for
a long press, as it does for the context hold. The web schedules one `setTimeout` to the router's next
deadline and calls `Tick(performance.now())`.

### 6b. A pointer event carries its time

`PointerEvent` gains `TimeMs`, Flutter's `timeStamp`: the moment the platform saw the event, in milliseconds
on a monotonic clock, so a recognizer measures a fling's velocity (#1560 needs it in 1.0, and widening the
record after 1.0 would cost a major). The unit is the same on every target and the origin is each target's
own, which is all a difference of two times needs.

- **Web**: the DOM event's `timeStamp` (a `DOMHighResTimeStamp`, milliseconds since the time origin, monotonic).
- **Photon**: the shell's own event time when it hands one to the host (the pointer entry points take an
  optional `timeMs`; the macOS shell passes `NSEvent.timestamp`, seconds since boot, as milliseconds), and
  the host's monotonic clock (`Stopwatch`) when it does not. A test passes its own times.

The record widens and its old constructor and `Deconstruct` go, in preview (`PointerEvent` has not shipped:
its entries leave the #1562 change's `PublicAPI.Unshipped` file). The time is pinned in the value twin's
fixture as every member is.

### 7. The sample: a long press that then drags, inside a scroll view

`samples/DefaultUIDashboard/Gestures/HoldThenDrag.cs`: tracks one pointer; `StartDeadline(ContextTarget.HoldMs)`
at the down; travel past `Touch.PressCancelSlop` before it rejects; the deadline accepts and reports
`OnHeld`; then each move reports `OnMoved(offset)` and the up `OnReleased(offset)`. A dashboard screen lays
cards in a vertical `ScrollView`, each wrapped in a `RawGestureDetector` with `HoldThenDrag` that moves the
card sideways, each holding a `Button`. Three outcomes, both targets: a hold then a drag moves the card and the
list stays; a swipe at once scrolls the list and the recognizer is rejected; a tap presses the button.

Photon's proof runs the SAME source: the Native engine tests compile `HoldThenDrag.cs` from the sample with a
linked `Compile Include`, .NET's own mechanism for one file in two projects, rather than a copy.

## Risks / Trade-offs

- **The browser's slop against a recognizer that accepts on travel** (web only). Not measured: whether a
  `touchmove` is still cancelable when a recognizer accepts at 12 px depends on the browser's own slop. Task 3.5
  measures it with real touch input in Chromium and writes the answer into the spec; the sample's gesture (a
  hold) does not depend on it.
- **A hold then a drag on iOS Safari**: the long-press callout and the text magnifier are the system's;
  `-webkit-touch-callout` and `user-select` on the detector are the lowering's to set if the e2e shows them.
  Not measured (no WebKit touch in the e2e today).
- **The press's `:active` look on the web** is not taken back when the press loses, where Photon's pressed
  token swap is. A visual difference, named in the row.
- **Restructuring `PressDown`'s early returns** so the arena opens on every path is the riskiest edit in the
  host; the existing host suites are the net.
- **One more document-level controller on the web**, installed only when a detector is lowered, as every
  other controller is.

## Open Questions

- **The arena's HOLD (a double tap).** Not in this slice, and recommended for 1.0 as the slice right after it
  (the coordinator, 2026-11-17, pending Edgar). This slice is built so that HOLD is an ADDITION: the arena's
  entry keeps the state a hold needs (open, closed, held, swept) as one value rather than two booleans, the
  sweep is one method a hold can defer, and no member's contract assumes the arena resolves at the up. On the
  web, a press that wins after a held arena needs its `click` replayed, a synthetic `click` on its element.
- **Fields, code and sheet surfaces as members.** Out of 1.0 (the coordinator, 2026-11-17): internal behaviour,
  and bringing them in later changes no signature. Their selection drags would have to wait for the slop to
  join, which changes how every text selection feels.
