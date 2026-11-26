## Context

See proposal.md, "Why". `docs/FLUTTER-PARITY.md` §5 has the row this change reaches:

- `FocusNode`, `FocusScope`, `FocusManager`: **PARTIAL**, "focus order and initial focus are expressible;
  there is no focus object to hold or move imperatively."

### What each target does today (read, not recalled)

**Photon.** One order exists: `RealizeResult.FocusStops`, built depth-first in paint order by the input
sink (`InputSink.AddStop`), suppressed under a composite (`WithoutFocusStops`), a table viewport's reordered
by cell (`OrderStopsFrom`). Each `FocusStop` carries its position (`Element`), its `Bounds` and what it is (a
`Pressable`, a `TextEntry`, an `Adjustable`, a `Navigable`, a code surface, a sheet, a table). The host keeps
the focus in several fields (`_focused`, `_focusedId`, `_focusedAt`, `_textAt` and the code and sheet
editing positions), funnelled through `FocusOn`, `ForgetFocus`, `BeginEditing` and `EndEditing`. Tab reaches
`FocusNext`/`FocusPrevious` → `MoveFocus`, which walks the stops, wraps, and keeps to the topmost modal layer
(`RealizeResult.ModalLayerRoot`, #1066), then `Land`s, the one way onto a stop that a screen reader's
activation shares. After every frame four adopters run: `AdoptAutofocus` (fields, code surfaces),
`KeepModalFocus` (`PhotonHost.ModalFocus.cs`: focus into a layer, the focus given back when it closes,
#1811/#1813), `AdoptFocusRequests` (code and sheet models' `FocusVersion`) and `AdoptCompositeFocus`
(`Navigable.FocusVersion`).

**Web.** No order is shared: Tab is the browser's own, and each module queries the DOM with a selector of
its own. `dom/focus-trap.ts` holds the trap: its FOCUSABLE selector, a window `keydown` that wraps Tab at the
topmost on-screen `[data-eq-trap]` (only for a Tab nothing consumed, #598), a `focusin` guard, the focus
into a layer (`data-eq-initial-focus`, then the first control, then the layer), and the return (#1813,
#1829), reconciled after each write (`commitFocusTraps`, `element-roots.ts` `afterWrite`). It asks the
adaptive gates when the key arrives (`onScreen`, #1833) and deliberately reads no geometry. The table
viewport walks its controls in cell order (`table-viewports.ts` `tab`), the sortable refocuses a moved item
by its id (`dom/sortable.ts`), a composite's host takes a version-driven focus request
(`CompositeFocusState`, `focusCompositeAfterRender` in `lowering.ts`), and a code surface keeps its own
`focusedVersions`.

**Nothing moves the focus by direction** on either target. `NavigableKeys` maps a key to a `NavigableMove`
and the component decides; `TreeView.Move` walks its own rows and the focus never leaves the composite's
one stop.

## How Flutter solves it, and in which tree

Flutter answers with a FOURTH tree beside its three, and the three decide its parts:

- A `FocusNode` is a long-lived object, owned by a `State` the way a controller is (`TextEditingController`,
  `ScrollController`): it outlives every rebuild of the widget that names it.
- The focus tree's SHAPE is made by the element tree (the second tree). `Focus` and `FocusScope` widgets
  attach their node in their `State` (`FocusAttachment`), and `reparent()` reads the nearest enclosing
  `Focus` from the `BuildContext`. A `Widget` (the first tree) only names the node; it never holds a
  parent.
- Traversal reads GEOMETRY from the render tree (the third): `node.rect` comes from the render object.
  The default `ReadingOrderTraversalPolicy` sorts by it; `WidgetOrderTraversalPolicy` walks the order of
  the element tree and needs none; `DirectionalFocusTraversalPolicyMixin` (`focusInDirection`) needs rects.
- `FocusManager` holds the primary focus, applies requests in a microtask (`_applyFocusChange`) and
  notifies every node whose `hasFocus` changed. A `FocusScopeNode` remembers its `focusedChild`, and its
  `traversalEdgeBehavior` (`closedLoop` by default, `parentScope`, `stop`, and on the web
  `leaveFlutterView`) says what happens past its last node. A modal route's scope makes the routes beneath
  it unfocusable, and the navigator gives the focus back to the route beneath when one pops.

So here: **the `FocusNode` is held by a component's state like a controller; the `Focus` and `FocusScope`
nodes (first tree, `VisualNode`) only carry a reference and gain no parent (PRINCIPLES 6); the attachment,
which node stands under which, is made at the POSITION, the `Element` on Photon and the DOM element on the
web (second tree), by the realizer's walk; and the geometry a directional move needs is read from the
`LayoutNode` on Photon and the element's box on the web (third tree), in slice 2.**

## Goals / Non-Goals

**Goals (slice 1):** the focus object and its tree; one `FocusManager` written once in C# and transpiled;
one traversal for Tab, Shift+Tab, `NextFocus` and `PreviousFocus` on both targets, the modal layer's Tab walk
included; requests and notifications; `Focus` and `FocusScope` nodes around any child; `Navigable.FocusVersion`
replaced; a dashboard screen moving the focus from code on both targets.

**Non-goals (later slices, each named in "Slices"):** `FocusInDirection` (slice 2); the trap's focus into a
layer and its return, and `Pressable.InitialFocus` (slice 3); the text field's and the surfaces' OWN focus
knobs, `TextEntry.Autofocus`/`OnFocusChanged`, `CodeSurface.Autofocus`, the surface models' `FocusVersion`,
and the table viewport's order (slice 4). A `Focus` around a field or a surface works from slice 1: its stop
is a stop like any other, and landing on it starts editing as a Tab does (`Land`); key events routed through the focus chain (`Focus.onKeyEvent`): `Shortcut` with
`FocusScoped` already answers for a subtree the focus is in, and `keyboard-shortcuts` keeps its row as
DIFFERENT.

## Decisions

### 1. Where the one traversal lives: two designs

**(a) One manager in Primitives, transpiled, fed by each realizer (CHOSEN).** `FocusManager` and the nodes
live in `src/eQuantic.UI.Primitives/Focus/`, which joins the transpiled model sources beside `Gestures/`
(`SharedComponentTranspilationTests.SharedModelSources`), so the browser runs the same code Photon runs, as
the gesture arena does (#1563). Each realizer hands the manager its stops IN ITS ORDER (Photon's paint walk,
the web's document order), each with the chain of focus nodes it stands under, and applies what the manager
answers. One fixture, `focus-traversal.fixture.json`, runs against the C# manager and its transpiled twin.

Costs: the web stops leaving Tab to the browser everywhere, not only inside a trap (decision 5), and builds a
snapshot of its stops when a Tab or a request arrives (one `querySelectorAll` over the one selector, plus a
`closest` per stop for its chain: what the trap already pays on every trapped Tab today); Photon's
`FocusStop` grows a chain (shared by every stop under the same nodes, so a steady frame allocates no list per
stop); the twin must behave like the C# (the fixture holds it; a construct eqc refuses is filed, PRINCIPLES
14). Residue after slice 1: the trap's focus into a layer and its return on both targets (slice 3), the
surfaces' versions (slice 4), each named below with the slice that removes it.

**(b) Per-target orders behind a shared contract (REJECTED).** The vocabulary declares `FocusNode` whose
`RequestFocus`, `NextFocus` and `PreviousFocus` call an `IFocusDriver` that each realizer implements over its
own order: Photon over `FocusStops`, the web over the DOM's.

Costs: next, previous, the edge behaviours, the trap and (slice 2) the directional move are written TWICE,
in C# and by hand in TypeScript, and a fixture can only hold each to the same expectations, never to the same
code (PRINCIPLES 4 is bent for good). `focus-trap.ts` and `PhotonHost.ModalFocus.cs` stay two mechanisms
forever, which is precisely what the request forbids. It is cheaper today (no Tab takeover on the web, no
snapshot; about 3 points less for slice 1), and dearer at every slice after it.

(a) folds the existing mechanisms with the least residue: after slice 3 the two trap files are drivers of
the manager, and after slice 4 nothing outside the manager decides where the focus goes.

### 2. Decisions vs writes

The rule that says what "one mechanism" means here: **every DECISION about where the focus goes is the
manager's** (Tab, Shift+Tab, a request, the trap, the edge of a scope, and from slice 2 a direction).
**A WRITE the target makes for its own reasons is ADOPTED** by the manager, never a second order: a press on
a control, the browser focusing a field it was clicked in, Photon's sort following a moved item
(`PhotonHost.Sorting`), the web's sortable refocusing it by id. Photon reports the position that holds the
focus (`_textAt ?? _focusedAt`, and the code and sheet editing positions) to the manager once at the end of
each frame's adoption; the web reports it from a `focusin`/`focusout` listener on the document (the
element's nearest stop). Changes are notified from the report, whatever caused them.

### 3. The manager's shape (realizer-facing)

```csharp
public sealed class FocusManager                    // one per host, as GestureRouter is
{
    public FocusManager(bool leavesViewAtRoot);           // Photon false (the root wraps), the web true
    public FocusNode? PrimaryFocus { get; }          // the innermost node owning the focused stop, or null
    public object? PrimaryPosition { get; }          // the position holding the focus, any stop's
    public void Attach(IReadOnlyList<TraversalStop> stops, FocusScopeNode? modal); // the frame's stops, in order
    public void Focused(object? position);           // the target moved the focus: adopt and notify
    public FocusMove Traverse(bool forward);         // where Tab or Shift+Tab goes
    public FocusMove? TakeRequest();                 // the move a RequestFocus/Unfocus asked for, once
    public event Action? RequestAsked;               // a node asked between frames
}
public readonly record struct TraversalStop(object Position, IReadOnlyList<FocusAttachment> Chain);
public readonly record struct FocusAttachment(FocusNode Node, Action<bool>? OnFocusChange);
public readonly record struct FocusMove(object? To, bool LeavesView);
```

`Position` is whatever identifies the stop's place across builds: the `Element` on Photon, the DOM element on
the web, as `GestureRouter.Recognizers(position, …)` takes. `Chain` lists the focus nodes the stop stands
under, outermost first, each with the latest `onFocusChange` its `Focus` or `FocusScope` node was built with.
A `Focus` given no node gets one from the realizer, kept at its position (Photon's element state, the web's
`nodeStateFor`), as Flutter's `Focus` creates one in its `State`. A modal, open layer contributes a
`FocusScopeNode` the realizer keeps at the layer's position, with `ClosedLoop`, and is passed as `modal` when
it is the topmost on screen (the web asks the adaptive gates, #1833, when it builds the snapshot).

The engineer may refine a realizer-facing name; the public vocabulary of decision 4 is fixed.

### 4. The public vocabulary

- `FocusNode` (class): `RequestFocus()`, `Unfocus()`, `bool HasFocus`, `bool HasPrimaryFocus`,
  `bool CanRequestFocus { get; set; }` (true), `bool SkipTraversal { get; set; }` (false), `bool NextFocus()`,
  `bool PreviousFocus()`, `event Action? Changed` (Flutter's `ChangeNotifier`; the idiom of
  `FormController.Changed`). `FocusInDirection(TraversalDirection)` is slice 2's.
- `FocusScopeNode : FocusNode`: `FocusNode? FocusedChild`, `TraversalEdgeBehavior TraversalEdgeBehavior
  { get; set; }` (`ClosedLoop`, Flutter's default). `RequestFocus()` on a scope focuses its `FocusedChild`,
  or its first traversable stop.
- `TraversalEdgeBehavior { ClosedLoop, ParentScope, Stop }`, append-only (`EnumValueAbiTests`). Flutter's
  `leaveFlutterView` is not a value: leaving the page at the root's edge is the web realizer's fact, not
  something an app chooses (PRINCIPLES 5: the word would not exist without a host page).
- `Focus(VisualNode child, FocusNode? focusNode = null, bool autofocus = false, Action<bool>? onFocusChange =
  null, bool? canRequestFocus = null, bool? skipTraversal = null)` and `FocusScope(VisualNode child,
  FocusScopeNode? node = null, bool autofocus = false, Action<bool>? onFocusChange = null,
  TraversalEdgeBehavior? traversalEdgeBehavior = null)`: capability nodes around any child (PRINCIPLES 15),
  each with its factory (`UI.Focus`, `UI.FocusScope`), its visitor member, node kind and coverage entries. A
  nullable knob given a value is written onto the node at every build, as Flutter's `Focus` does; left null,
  the node's own value stands.

### 5. What a node stands for, and the rules of the walk

- **Own stops.** The stops under a `Focus` whose innermost `Focus` it is are its own. `RequestFocus` lands on
  its first own traversable stop; `HasPrimaryFocus` is true while one of its own stops holds the focus;
  `HasFocus` while any stop under its position does. In the common case (`Focus` around a `Button`) a node
  has one own stop and these are Flutter's answers exactly. A `Focus` with several own stops (around a row
  of buttons) is a group: Flutter would focus the node itself, which no target can show; this is the one
  DIFFERENT detail the parity row names.
- **A `Focus` around no stop makes its child one** (Flutter's custom focusable): the web gives the `Focus`'s
  host element `tabindex="0"`, Photon records a `FocusStop` for its box. That is what a `FocusScoped`
  shortcut inside it needs to answer.
- **Traversable**: a stop whose owner (if any) has `CanRequestFocus` and not `SkipTraversal`, and, while a
  modal scope is passed, that stands under it. `SkipTraversal` leaves `RequestFocus` working.
- **The walk** (Flutter's `WidgetOrderTraversalPolicy`, geometry-free, so the web flushes no layout):
  1. its scope is the innermost `FocusScopeNode` in the primary stop's chain, the root when there is none,
     and the modal scope when the primary stop is outside it;
  2. its candidates are the traversable stops under that scope, in the realizer's order;
  3. from no primary stop, or one outside the modal scope, it enters at the first (forward) or last
     (backward) candidate;
  4. past the last candidate (or before the first), the scope's edge decides: `ClosedLoop` wraps, `Stop`
     stays, `ParentScope` goes on in the enclosing scope from the scope's place; at the root, Photon wraps
     (today's walk), and where the manager was made with `leavesViewAtRoot` (the web) the move leaves: the
     driver lets the browser's own Tab take the focus out of the page, as Flutter's web root does with
     `leaveFlutterView`;
  5. a modal scope with no candidate holds the keyboard: the move goes to the scope's own position, which
     the web focuses (the layer carries `tabindex="-1"`) and Photon, having no stop there, answers by
     clearing the focus. Today's behaviour on both targets (`modal-focus`).
- **Requests.** `RequestFocus` on a node the manager has attached queues a move and raises `RequestAsked`;
  on a node not attached yet it is remembered on the node and honoured at the first attach that contains it
  (today's "a request made before the surface was first drawn is honoured when it is"). A request is applied
  AFTER the write that follows it: Photon in the frame's adoption (where `AdoptCompositeFocus` ran), the web
  in the `afterWrite` pass where `commitFocusTraps` runs (#1829), so a field the same `SetState` removes
  is gone before the focus moves (the reason `focusCompositeAfterRender` waited). Of two requests before one
  apply, the last wins, as Flutter's `_applyFocusChange` takes the last. `Unfocus()` on a node that has the
  focus clears it (Photon `ClearFocus`/`EndEditing`, the web `blur`), Flutter's default
  `UnfocusDisposition.scope` with nothing left focused.
- **Autofocus.** `Focus(autofocus: true)` asks once per position, at its first attach, unless the focus is
  already inside its nearest scope (Flutter's rule). Slice 3 makes the layer's initial focus this rule.
- **Notification.** After every report or applied move, every node whose `HasFocus` or `HasPrimaryFocus`
  changed raises `Changed`, and its attachment's `onFocusChange(HasFocus)` is called, outermost first. A
  stop that leaves the tree while focused is a report of "nothing", which notifies.

### 6. The web takes Tab

The driver (`dom/focus-manager.ts`) listens to `keydown` on the window, in the bubble phase, for a Tab
nothing prevented (the rule of `modal-focus`, #598, unchanged), asks `Traverse`, prevents the default and
focuses the answer, or lets the default run when the move leaves the page. Its snapshot is the one FOCUSABLE
selector moved out of `focus-trap.ts` with its filters (`[hidden]`, `[aria-hidden="true"]`,
`[data-eq-exiting]`, the adaptive gates), in document order. The order equals the browser's own sequential
order as long as no positive `tabindex` exists and the selector names every focusable the SDK writes; that
is a claim NOT measured yet, and task 4.5 measures it (the driver switched off, the browser's Tab walked over
every dashboard screen in Chromium, compared with the manager's order, the test failing on any difference).

### 7. Measured: what C# events do when a handler changes them mid-dispatch

`FocusNode.Changed` is a C# event, and the twin must dispatch it as .NET does: a dispatch calls the
handlers that were subscribed when it began (see "Measurements" for the measured answer).

## Slices

The whole capability is about 29 points; each slice ships and is proved on both targets on its own.

| Slice | Points | What it delivers | What goes |
|---|---|---|---|
| **1, this change** | 8 | the object, the manager, the tree order, Tab on both targets through it with the modal walk folded, requests, notification, `Focus` and `FocusScope`, the sample | `Navigable.FocusVersion`, `AdoptCompositeFocus`, `CompositeFocusState`'s version, the trap's Tab handler on both targets |
| 2 | 5 | `FocusInDirection(TraversalDirection)` (`Up`, `Down`, `Left`, `Right`), Flutter's directional policy over the stops' rects, which `TraversalStop` gains (Photon's `Bounds`, the web's box read only when a direction is asked) | nothing |
| 3 | 8 | a scope traps AND restores: the layer's initial focus becomes autofocus inside its scope, the focus given back when it closes becomes the scope beneath restoring its `FocusedChild` (#1811, #1813, #1829); `focus-trap.ts` and `PhotonHost.ModalFocus.cs` become thin drivers | `Pressable.InitialFocus`, `Button.InitialFocus` (migration: `Focus(autofocus: true, child: …)`), the `data-eq-initial-focus` mark, the focus-in guard as code of its own |
| 4 | 8 | text fields, code and sheet surfaces and the table viewport join: each holds or is handed a `FocusNode`, the table viewport's cell order is the realizer's snapshot order on the web as on Photon | `TextEntry.Autofocus`, `TextEntry.OnFocusChanged`, `CodeSurface.Autofocus`, the surface models' `FocusVersion`/`FocusChanged`, the controllers' `RequestFocus()` (replaced by a held `FocusNode`), the table viewport's own `tab()`; the parity row reads SAME |

## Risks / Trade-offs

- [The web's Tab order changes where the selector misses a focusable the browser knows (an `iframe`, a
  `contenteditable`, `summary`, a media element with controls)] → the selector is widened to the HTML
  standard's focusable areas the SDK can write, and task 4.5's comparison fails on any difference.
- [Overlay order differs per target: Photon paints layers after the page, the web keeps them where the DOM
  puts them] → inside a modal layer the walk is the layer's alone; for a non-modal layer (a toast) the order
  is each target's, as it is today. Recorded in the spec as each target's tree order.
- [A request on Photon waits for the next frame] → `RequestAsked` sets `NeedsRender`, as a code surface's
  request does today.
- [The twin diverges from the C#] → one fixture, run by both suites; each case fails when the walk uses the
  most plausible wrong rule (the edge wrapping at the root for a `Stop` scope; `ParentScope` restarting at the
  parent's first instead of after the scope).
- [Slice 1 ships with the trap's restore and the surfaces' versions still beside the manager] → each is a
  WRITE the manager adopts, not a second order, and its removal is a named slice (3, 4); the parity row says
  so.

## Migration Plan

One break in slice 1: `Navigable.FocusVersion` goes. Migration line for the release notes and the wiki's
Upgrading page: *a composite that took the keyboard back with `FocusVersion = ++_version` holds a
`FocusNode`, wraps the `Navigable` in `Focus(focusNode: _focus, child: …)`, and calls
`_focus.RequestFocus()`.* `PublicAPI.Unshipped.txt` of Primitives gains the vocabulary and two `*REMOVED*`
lines; Components' gains `UI.Focus` and `UI.FocusScope`. `developer-surface.baseline.txt` does not move.

## Measurements

- **C# event dispatch, MEASURED** (`dotnet fsi`, .NET 10.0.9; a C# field-like event modelled exactly: `+=` is
  `Delegate.Combine` on the field, `-=` is `Delegate.Remove`, `Changed?.Invoke()` reads the field once and
  invokes it). Handlers H1 and H2; H1 removes H2 and adds H3; two dispatches. Answer: `H1,H2 | H1,H3`. The
  delegate a dispatch invokes is immutable, so a change made during it takes effect at the next one. A
  handler added twice is called twice, and one `-=` removes one of them (added twice, fired, removed once,
  fired: 3 calls). Both answers are scenarios (the spec's "A handler that removes itself") and fixture cases
  the twin is held to (task 2.3). A C# run of the same case through the build lock
  (task 2.3) confirms it on the language itself.
- **The web's Tab order equals the manager's**: NOT measured; task 4.5 measures it.

## Open Questions

- Whether `Pressable.CanRequestFocus` and `TableViewport.CanRequestFocus` go in slice 4 in favour of
  `Focus(canRequestFocus: false, child: …)` (PRINCIPLES 15 says a capability is a node; their parity row reads
  SAME with Flutter's `InkWell.canRequestFocus`, and the press's "keep the keyboard where it is" would move
  onto the `Focus` on both targets). Does not change slice 1.
- Whether `FocusNode` exposes its tree (`Parent`, `Children`, `EnclosingScope`) publicly. Slice 1 keeps it
  internal; nothing in the catalog needs it.
