# The Element tree, and the theme it carries

> Decided by Edgar on 2026-10-14 for 1.0 ([#898](https://github.com/eQuantic/equantic-ui/issues/898)):
> the Element is built before the release ([#169](https://github.com/eQuantic/equantic-ui/issues/169)),
> and per-component theme slots ship in it ([#186](https://github.com/eQuantic/equantic-ui/issues/186)).
> This plan is the design the code follows, and no code is written before it is read. Section 2 was
> re-measured that day on main (`209a88cd6`) and supersedes the numbers of
> [ARCHITECTURE-AUDIT.md](ARCHITECTURE-AUDIT.md) section 5, which it started from.

## 1. What is decided, and why now

1.0 promises a contract: an app written against it keeps compiling and keeps answering the same
through the 1.x line. Two of the shapes it freezes are the context every `Build` receives and the
theme a component reads. The Element changes the first, from a bag of values into a position; the
slots change the second, from foundation tokens alone into a slot per component. Either change after
the cut costs a major, so both are taken before it.

The two were offered with a cheaper road each, and the road was declined, not the mechanism: the
Element deferred behind a contract with its seams taken out of it, and the slots arriving in 1.x as
default members. Both mechanisms appear below anyway, because the design uses them.

*How does Flutter solve it?* With three trees: the configuration (`Widget`), the persistent instance
(`Element`, which IS the `BuildContext`), and the render tree (`RenderObject`). We have the first
(`VisualNode`) and the third (`LayoutNode`). The middle one exists too, spread across string-keyed
stores and ambient channels, and this plan gathers it into one tree.

The two decisions are one design because of a single line of Flutter: `Theme.of(context)` walks up
from a position. A slot resolved per component needs a theme found per position, and that needs the
Element.

---

## 2. What stands in for the Element today

### Identity is four strings

A component, or the state of a node, is found by a string, and each target spells it its own way.

| Where | The string | Formed by |
|---|---|---|
| Photon | a path, `r/0/2/[key]`, and a component's identity `"{path}#{type}#{key}"` | `LayoutEngine.ChildPath`, `LaidOutChildren.PathOf`, `ComponentInstanceStore.Reconcile` |
| Server (SSR) | `Type#ordinal`, the ordinal counted per type in depth-first order | `ComponentExpansionScope.Enter`, `ComponentIdentity.Key` |
| Browser | `path#type#key` again, with a new root (`r0`, `r1`…) at every stateless boundary | `ComponentInstanceStore` in `instance-store.ts`, `lowerVisualNode` |
| DOM | `(tag, key, child index)` among siblings | the reconciler |

None of them is parsed; each is compared or looked up. Photon builds a child path at 24 places,
uses a path as a store key at 9 more in layout and 25 at paint, and scans the frame's regions by
string equality on 29 lines of `PhotonHost`, whose one parser (`IsAncestorPath`) decides focus
within. The browser's `lowering.ts` builds a path at 36 places and keys something by it at 17, of
seven kinds: retention, DOM attributes, module maps read after the commit, the code editor's caret,
element ids, the focus scope and one reconciler key. The server has no path at all: its components
are named by type and order.

A string is only as stable as what produced it. A Photon path renumbers when an earlier sibling
appears; a browser root renumbers when an earlier stateless boundary does; a server ordinal shifts
when an earlier component of the same type is added. The browser says it in its own comment: the
roots are "stable while the page structure is stable".

### State lives in stores, and most of them never forget

Photon keeps per-position state in six stores, twelve dictionaries and one set, lent each frame to
the layout pass: `ComponentInstanceStore`, `TransitionStore`, `ScrollStore`, `PresenceStore`,
`DragStore` and `InViewStore`. Two of them prune what left the tree, the instance store at the end
of a pass and the presence store at the end of a frame. The other four keep an entry for every path
they have seen, and a different node that later takes the same path inherits it: a list's scroll
offset, an animation's tracks, a drag, a visibility. So does the host's map of the code editor's
revealed versions, and the path cache, which nothing clears.

The browser keeps its stateful instances in a store per page and per island, its scroll, canvas and
in-view registries in module maps cleared per commit, and per-element state as expandos on DOM nodes.
The server keeps no instance: each request builds a fresh tree, and between two prefetch rounds it
carries a component's state across by copying its fields by name onto the next round's instance.

### Context is one object per walk, and channels around it

`ComponentContext` is a sealed class with a public constructor, holding the theme, the type scale,
the density and a text measurer. One instance serves a whole walk: a frame on Photon, a lowering on
the server. Its other members read ambients: `Route` reads `RouteValues.Current`, `InFlow` reads
`InFlow.Current`, `GetService<T>` reads `CapabilityScope`.

Those ambients are twelve `AsyncLocal` channels: seven in Primitives (`CapabilityScope`,
`RouteValues`, `InFlow`, `FaceResolution`, and three on `ComponentBoundary`) and five in Web (the
expansion scope, the style sink, the gradient sink, the reported density and the link policy). Two
more are process-wide statics with public setters: `Navigator.Handler`, where "the last host to take
it wins", and `UiDispatcher.Current`. The browser holds the same things as module globals.

### Work is the whole tree, every time

On Photon a `SetState` sets `NeedsRender`, and the next frame re-measures and re-builds the whole
tree from the root: there is no dirty subtree. `Build` runs inside measure, and more than once a
frame wherever a container measures twice (a stack, a box reflowing, a flex slot, a wrapped flex, a
grid probing its columns). Intrinsic sizing builds a component a third time, without reconciling
it, so a stateful child is measured with a fresh instance's state. The frame budget (`PerfHarnessTests`)
is a pooled ceiling of 72 KB, measured on a scene with no stateful component, so it says nothing of
the identity strings and store writes above.

On the server a prefetch round rebuilds the whole tree, up to five rounds. In the browser a nested
component's `SetState` calls its owner's invalidator, and the owner, a page or an island, lowers all
of itself again.

### What it already costs

Each of these is the string model's, and each goes with it:

- A stateful root gains one `StateInvalidated` handler on every Photon frame (#935): the instance
  store fires `InstanceRetained` again for an instance it already holds, and the host subscribes on
  every firing. Measured: 101 handlers after 100 frames.
- A node that takes another's path inherits its scroll offset, its transition tracks and its drag.
- In the browser a shell component is a new instance on every client navigation, mounted before the
  old one is unmounted. The layout survives only as DOM.
- Hot reload in the browser keeps the page root's fields and drops every nested component's.
- On the server, two prefetch rounds agree on a component only through a by-name field copy.

### What the pins say

`FlutterParityPinTests` asserts the gap: no `Element`, `BuildContext` or `ElementTree` type, no
`DependencyScope` or `InheritedScope`, and `GetService` on `UiComponent` as the stand-in for
`InheritedWidget`. Each slice below flips the rows it closes, in the same pull request.

---

## 3. The design

### 3.1 An element per node

An element is a position: a parent, a slot among its siblings, the configuration it holds now (a
`VisualNode`) and its children. Every node gets one, as in Flutter, and not only the components,
because the state that hangs off a position belongs to nodes as much as to components: a scroll
view's offset, a presence's entrance and exit, a draggable's drag, an in-view's visibility, a
transition's tracks, focus and the caret.

Three kinds cover the vocabulary:

- **`ComponentElement`**, for a `StatelessComponent` or a `StatefulComponent`. It builds.
- **`InheritedElement`**, for an `InheritedComponent` (3.4). It holds dependents.
- **`NodeElement`**, for every node of the vocabulary. It holds the node's own state slot, typed per
  node (`ScrollState` on a scroll view's element, `PresenceState` on a presence's, and so on).

A rebuild hands an element a new configuration, and Flutter's rule decides what happens: the same
runtime type and the same `Key` update the element in place; anything else unmounts it with its
subtree and mounts a new one. Keyed siblings are matched by key, unkeyed ones by slot. The state that
was a dictionary entry is a field of the element, so it dies with the element, and no other node can
inherit it.

One state outlives its element on purpose: a `Presence` that leaves animates out. Today the presence
store keeps the departed subtree's last draw commands and replays them until the exit finishes
(`PresenceMotionTests.Realizer_DepartedPresence_ReplaysAsPixelsOnly_ThenSettlesClean` pins it). With
elements the departure is the same, moved off the path: when a presence's element unmounts, its
components unmount at once and it takes no more input, and its last paint is handed to the owner as
a detached, paint-only exit, replayed at the departed position until the animation completes or the
user asked for reduced motion. Nothing in the exit can be built, pressed or focused, which is what
the test already asserts.

A `StatefulComponent` keeps the shape the parity audit calls SAME: one class, not two. Its element
retains the first instance it mounted, and every rebuild's fresh instance hands over its
configuration through `AdoptConfig`, which is what the instance store does today, keyed by element
instead of string. `OnMount` and `OnUnmount` run when the element mounts and unmounts, in that order
across a pass: what leaves unmounts before what arrives mounts.

### 3.2 Build leaves layout

Today `Build` runs inside measure. With elements it runs in a build phase of its own, before
layout, and only for the elements marked dirty, as Flutter's `BuildOwner.buildScope` does. A
`SetState` marks its own element dirty and asks the host for a frame; the frame rebuilds the dirty
elements, then lays out and paints.

The dirty elements build shallowest first, ancestors before descendants, as `buildScope` sorts them by
depth: a parent that rebuilds hands its children their new configuration (through `AdoptConfig` for a
retained instance) or removes them, so a child built before its parent would be built twice or with
a stale configuration. A child its parent's build already rebuilt is no longer dirty and is skipped,
one that left the tree is dropped, and one marked dirty during the phase joins it in depth order.

Layout measures what the elements already built and builds nothing. A container that measures a
child twice measures one built subtree twice, and intrinsic sizing reads that subtree too, so a
stateful child is measured with its own state. A `NodeElement` owns its `LayoutNode` and updates it
in place, where today a
frame produces a new tree of them; whether the pool is still needed afterwards is measured, not
assumed (section 5).

Nothing an app writes asks for constraints during `Build`, so moving it out of layout costs no
author anything. The one consumer that will (`LayoutBuilder`, which the parity audit lists as
blocked) gets a build in layout as an explicit element kind later, as Flutter's `LayoutBuilder`
does, not as the rule for every component.

### 3.3 The context is the element

`Build(ComponentContext context)` keeps its signature. What changes is what the argument is: one
`ComponentContext` per component element, created when the element mounts, a view of that position.

Every member it has today stays and becomes positional. `Theme` is the nearest `Theme` above it, or
the app's; `Route` is the page's route; `InFlow` is whether an interaction wrapper is above;
`Density` and `TypeScale` come from the root; `GetService<T>` resolves through the nearest services
above, which is the root's unless a subtree provides its own; `MeasureText` and `MonoAdvance` keep
the host's measurer.

Two members are added:

- **`DependOn<T>()`**, where `T` is an `InheritedComponent`: the nearest one above, and a
  registration, so this element rebuilds when it changes. `Theme`, `Route` and `InFlow` are read
  through it, so reading the theme makes a component a dependent of it.
- **`Find<T>()`**: the nearest one, with no registration, for a value read once.

The constructor leaves the public surface. A context is a position and only an owner can make one.
A test that needs one mounts a component through the testing harness (#294) instead of calling
`new ComponentContext(theme)`, which about 25 tests do today. So the constructor goes in the slice
that follows the harness, not in E1: until then the owner makes contexts and the tests keep theirs.

### 3.4 Inherited components, and what depends on them

`InheritedComponent` is a vocabulary type: a node with a child that holds a value for its subtree.
It answers `UpdateShouldNotify(old)`; when that is true after an update, every element that called
`DependOn` on it is marked dirty, and nothing else is. A `StatefulComponent` gains
`OnDependenciesChanged()`, called before its next build when one of its dependencies changed, as
Flutter's `didChangeDependencies`.

This is what turns the host-wide invalidation of section 2 into a targeted one. A navigation
changes the route; only what read the route rebuilds. A theme switch changes the theme; only what
read the theme rebuilds, which in practice is everything that draws, and that is correct.

The first inherited components are the SDK's own: `Theme`, the page's route, the interaction
wrapper's `InFlow`, and the services. An app's own come next, and they are what `#289` (shared state
beyond a component) builds on.

### 3.5 From ambient to positional

| Today | Becomes | Why it moves, or why it stays |
|---|---|---|
| `CapabilityScope` (`AsyncLocal`, set per request on the server, once per app on Photon) | the root element's services, overridable by a subtree | a capability is resolved from a position; the server's middleware outside the tree keeps the channel for server actions |
| `RouteValues.Current` (`AsyncLocal`) | an inherited route at the page root | a navigation rebuilds only its dependents |
| `InFlow.Current` (`AsyncLocal`, saved and restored around a child) | an inherited value set by the interaction wrapper's element | it is a subtree value, which is what an inherited component is |
| `ComponentBoundary` diagnostics, report and tally (three `AsyncLocal`s) | the owner's (3.6) | they belong to one tree, not to an async flow |
| `FaceResolution` (`AsyncLocal`) | stays | it is the text services' diagnostic, outside the tree |
| `Navigator.Handler` (public static, "the last host wins") | `Navigator.Of(context).Go(destination)`, the host found from the root | two windows in one process each navigate their own |
| `UiDispatcher.Current` (public static) | the owner's dispatcher | a `SetState` from a worker posts to the tree it belongs to |
| the five Web channels (expansion scope, style sink, gradient sink, density, link policy) | the server's owner for one request | they are one render's, and the request is the owner |
| the browser's module globals (active pass, ordinals, walk depth, theme, density, in-flow) | the browser owner's | the same, in the browser |

Under SSR the reason for `AsyncLocal` was concurrent requests on shared instances. An owner per
request removes the reason: nothing component-shaped is shared between requests, and nothing is
looked up from a flow.

### 3.6 The owner

Each tree has one owner, after Flutter's `BuildOwner`: the dirty list, the build phase, the
pass's unmount-then-mount order, the error boundary's report and tally, the dispatcher, and the
diagnostics. A Photon host owns one per window, the server one per request, the browser one per
document and one per island (`VisualNodeComponent`), and an email render one per message. The
browser's is the document's and not the page's, so it outlives a client navigation.

### 3.7 Per target

- **Photon.** The host keeps one element tree across frames. A frame runs the build phase for the
  dirty elements, then layout and paint over the `LayoutNode`s the elements own. The six stores
  become state slots on elements. The host's path fields (pressed, focused, the text being edited,
  the hover chain, the pan and the drag) point at elements, the hit regions carry an element
  instead of a path, and focus-within walks the parents instead of comparing prefixes. The shells'
  accessibility ids, which are paths today, become the element's id: an integer assigned at mount
  and kept while it lives.
- **Server.** A request mounts a tree, builds it, and lowers it. A prefetch that completes marks its
  element dirty and rebuilds that subtree, with the element's own instance, so the by-name field
  copy between rounds goes.
- **Browser.** The runtime keeps the same element tree. Hydration builds it from the page and walks
  it against the server's DOM in the same order as today. The per-page stores, the module maps and
  the path attributes become element state and element ids.

  A client navigation changes the page's type, and by the rule of 3.1 that replaces the page's
  element with everything the page built. The samples build their frame inside each page today
  (`ConsoleShell.Frame(…)` in every screen's `Build`, the sidebar's open state a field of each page),
  so for them a navigation stays what it is now: the DOM is patched position by position, and no
  instance survives. What survives a navigation is what sits ABOVE the page. *How does Flutter solve
  it?* go_router's `ShellRoute`: the shell is a route of its own, and the pages are its child. Here the
  app declares its shell once, in `Program.cs`, as a component with a slot the router fills with the
  page; the shell's element and its instance stay mounted across navigations, the page's element is
  replaced under it, and the old page unmounts before the new one mounts. E3 moves the dashboard
  sample's frame there, which is its proof.
- **Email.** A message mounts a tree once, lowers it and discards it.

### 3.8 The strings that stay, at the seams

Some identities cross a boundary, and a boundary needs a string. They stay, computed from the
element when crossing and never used as an address inside a tree:

- the hydration payload's key, `Type#ordinal`, which the server and the browser already agree on;
- hot reload's capture, which can now carry every stateful element's fields instead of the page
  root's alone. Not under `Type#ordinal`: the hydration key holds because the server and the browser
  render the same tree, and an edit is exactly what changes the tree between a capture and its
  replay, so an inserted component or two keyed siblings that swapped would hand one component
  another's fields. The capture keys each element by its position as a chain from the root, every
  step its component type and its key or, unkeyed, its slot among siblings of that type. The replay
  restores an element only when its chain and its type match exactly, and an element with no match,
  or with more than one, starts fresh;
- the shells' accessibility ids, as the element's integer id;
- the visual editor's `VisualNode.Origin`, which identifies source, not position, and is unchanged.

### 3.9 Written once, transpiled

The instance store exists twice today, in C# and in TypeScript, and the two already differ: one
store per host against one per page and island, a root `r` against `r0`, `r1`…, an event against a
hook. The element engine is written once, in C# in `eQuantic.UI.Primitives`, and transpiled into the
runtime like the code editor's engine (`eQuantic.UI.Code`). The realizers drive it through a narrow
interface, as they drive the code editor through `ICodeSurfaceModel`, and none of them reimplements
it. The first slice measures what stops eqc from transpiling it, and closes that before anything
else depends on it.

### 3.10 The theme slots

Measured: of the 58 components, 51 read the theme. What they read is foundation: colours by role,
`Colors(variant)`, `Shape`, `Type` and `Elevation`. What makes each one look like itself is written
in the component, as constants and literals (the button's minimum width of 64, the tab bar's height
of 48 and its indicator of 3, the dialog's maximum width of 480 and its paddings, the chip's label at
13 over 16 in weight 600), and in `Sizing`, a static ladder of control sizes no theme reaches.
`MaterialTheme` repaints every colour and every radius and cannot change a control's height. No
component takes a style of its own: an author's knobs are selectors (`Variant`, `SizeVariant`, a
kind), not values.

**What a slot is.** One record per component family (`ButtonTheme`, `InputTheme`…), every member
nullable, meaning "the component's own default", and every member data: numbers, tokens, enums, and
tables keyed by `SizeVariant`, `Variant` or `Density` where the component varies by them. No
delegate, because a slot crosses to the browser as JSON (below).

`Density` is not optional where the component reads it today: the ladder's height, padding, label
size and hit target vary by size AND density (a medium control is 40 comfortable and 32 compact), and
so do the switch and the selection box. A slot entry for such a value holds both densities, the
component picks with `context.Density` as it does now, and the browser's JSON carries both, because
the density is the browser's to report and can change after the server wrote the shell.

**What it carries.** What the component hard-codes that is LOOK: heights, paddings, gaps, minimum
and maximum sizes, border widths, the shape, the colours per state, the label's type and the
elevation. What is BEHAVIOUR stays the component's: thresholds, durations, counts and windows
(`PullToRefresh.Threshold`, `Pagination.Window`).

**Where it lives.** On `IAppTheme`, as a default member per slot that answers null
(`ButtonTheme? Button => null`), which is how `MonoFamily`, `Data` and `Code` arrived: every theme an
app wrote keeps compiling, and eqc carries a default of `IAppTheme` into a theme's twin (EQ1008). The
SDK's two themes fill them: `PhotonTheme` with today's constants, which become the design system's
instead of each component's, and `MaterialTheme` with Material 3's component specifications. One
slot is shared rather than per family: `Controls`, the ladder `Sizing` holds today (height, padding,
label size, gap, icon and radius per `SizeVariant`, and per `Density` where `Sizing` varies by it),
so a theme that changes how tall a control is changes every control of that size at once, which is
the rule `Sizing` states.

**The order.** A component resolves each value, and the first that answers wins:

1. the instance's own argument, where the component has one;
2. its family's slot in the nearest `Theme` above it (`context.Theme` is positional, E5);
3. for a sized control, the `Controls` slot of that theme;
4. the component's default, today's constant or `Sizing` rung.

At every step a value that varies by density is read at `context.Density`, so a slot never bypasses
the compact layout the fallback would have given.

The precedent is already in the tree three times: `Text.StyleOverride ?? theme.Type(role)`, a code
decoration's colour or its kind's, and `Markdown.Style ?? new MarkdownStyle()`.

**A subtree.** `Theme(theme, child)` overrides the theme below it, and `theme.With(button: …)` builds
the override from the current theme with one slot replaced. `With` is the SDK's, a member that
forwards every other member, defaults included. The decorators apps write today forward the
abstract members and leave the defaults to the interface: one wrapped around `MaterialTheme` would
lose its slots.

**Crossing to the browser.** The browser builds its theme from the JSON the server writes into the
shell, not from the app's theme class. The slots cross in it, and so must everything a theme
answers. Today `Code` does not: a code palette an app overrides reaches the server and Photon and not
the browser (#936). E6 holds the rule for every member of `IAppTheme`, and the bridge's pinned JSON
proves it.

**The families for 1.0**, from what the components hard-code today:

| Slot | Components it styles |
|---|---|
| `Controls` | every sized control (the `Sizing` ladder) |
| `Button` | Button, and the Buttons that Dialog, Banner, Pagination and the forms build |
| `IconButton` | IconButton |
| `Input` | TextInput, SearchField, Select, TimePicker |
| `Selection` | Checkbox, RadioGroup, Switch, Slider |
| `Segmented` | SegmentedControl, Stepper |
| `Chip` | Chip |
| `Navigation` | Tabs, BottomNavigation, NavigationRail, AppBar, Breadcrumb, Pagination, PageIndicator |
| `Surface` | Dialog, BottomSheet, Drawer |
| `Menu` | Popover, Menu, Tooltip |
| `Notice` | Toast, Banner, CookieConsent |
| `Progress` | ProgressBar, PullToRefresh, Skeleton |
| `Card` | Card |
| `List` | ListItem, List, Divider, Accordion, SwipeableRow |
| `Table` | Table, DataTable |
| `Badge` | Badge, Avatar, EmptyState |
| `Calendar` | Calendar, DatePicker |

The code editor, the document and the charts already have theirs (`Code`, `MarkdownStyle`, `Data`),
and E6 brings them under the same rules: `MarkdownStyle` becomes a slot, and all three cross.

### 3.11 What an app sees

- **Unchanged:** `Build(ComponentContext context)`, `SetState`, `OnMount`, `OnUnmount`,
  `AdoptConfig`, `Key`, the factories, and every member `ComponentContext` has today.
- **New:** `InheritedComponent`, `ComponentContext.DependOn<T>()` and `Find<T>()`,
  `StatefulComponent.OnDependenciesChanged()`, `Theme(theme, child)`, the slots on `IAppTheme`,
  `Navigator.Of(context)`, and a shell declared once in `Program.cs` (3.7).
- **Gone from the public surface:** `ComponentInstanceStore`, `NotifyMounted`, `NotifyUnmounted`
  and `StateInvalidated` (the host's, never the app's), the constructor of `ComponentContext`,
  `Navigator.Handler` and the static `Navigator.Go`, and the setters of `UiDispatcher.Current` and
  `RouteValues.Current`. Nothing in `samples/` calls any of them.

The preview's rule applies: each goes in the pull request that replaces it, and each is a
`*REMOVED*` line in the release's notes.

---

## 4. Slices

Each slice is its own OpenSpec change and pull request, and each proves itself the same way: the
outputs already pinned stay byte-identical (the SSR fixtures, the goldens, the transpiled pins and
the hydration contract), the slice's new behaviour has tests that fail on the code before it, and
the parity rows it closes flip.

1. **E1, the engine and Photon's instances.** `Element`, the three kinds, the owner and the build
   phase, written once in C# and transpiled; Photon mounts its tree through it, and
   `ComponentInstanceStore` goes. Closes #935 by construction. Proof: the native suites and goldens
   unchanged, retention by element across rebuilds and keyed moves, the unmount-then-mount order,
   and the frame budget measured on a scene that has stateful components.
2. **E2, Photon's node state.** The five other stores become state slots, the presence exits move
   to the owner as detached, paint-only replays, the host's path fields become element references,
   the shells' accessibility ids become element ids, and the path cache goes. Proof: a node that
   takes another's position starts with its own state, and the presence tests pass unchanged.
3. **E3, the browser.** The runtime's stores, module maps and path attributes move onto the
   transpiled engine; hydration builds the element tree; the shell an app declares in `Program.cs`
   stays mounted across client navigations, and the dashboard sample moves its frame into it. Proof:
   the runtime's own suites, the hydration contract, the sidebar's state across a navigation, and a
   page that still builds its own frame patching as it does today.
4. **E4, the server.** An owner per request; a prefetch rebuilds its subtree; the five Web channels
   and the Primitives ones the tree owns move to the owner. Proof: the SSR fixtures unchanged, and a
   prefetch round that keeps a component's instance.
5. **E5, inherited components.** `InheritedComponent`, `DependOn`, `Find`,
   `OnDependenciesChanged`, dependency-driven rebuild, `Theme`, the route and `InFlow` as inherited
   components, `Navigator.Of`. Proof: a change rebuilds its dependents and nothing else, on both
   targets.
6. **E6, the theme slots.** Section 3.10, on the inherited theme of E5.

E1 to E5 are [#937](https://github.com/eQuantic/equantic-ui/issues/937), and E6 is
[#938](https://github.com/eQuantic/equantic-ui/issues/938), both under the 1.0 epic.

E1 to E4 change no public signature an app calls; E5 and E6 add to it.

---

## 5. Measured before each slice

- **What stops eqc from transpiling the engine** (E1), before the engine is written against it.
- **Allocation per frame** with a stateful scene added to `PerfHarnessTests` (E1 and E2), before and
  after, and whether the `LayoutNode` pool still earns its place once nodes are updated in place.
- **Allocation per request** on the server, before and after (E4).
- **The served runtime's size**, which its budget fails (E3).
- **Hydration on a real browser.** `tests/e2e` proves it today but no CI job runs it (#286); E3
  runs it by hand before and after, and says so in its pull request.

---

## 6. Not in this plan

- `GlobalKey`, the element that moves between parents. Nothing needs it yet.
- `InheritedModel`'s aspects, a dependency on part of a value. The first inherited components are
  small enough that rebuilding all their dependents costs nothing worth the API.
- `FocusManager` and the rest of `PhotonHost`'s split (the audit's step 11): the element tree makes
  it easier, and it stays its own work.
- `LayoutBuilder` (3.2), after E2.
