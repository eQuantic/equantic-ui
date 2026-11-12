# element-tree Specification

## Purpose
How a live tree keeps what belongs to a position (a component's instance, a node's state) by the
position itself, when its components build, and in what order they mount and unmount, so state belongs
to where it stands rather than to a string a frame happens to spell the same way.

## Requirements

### Requirement: A position keeps its stateful instance

On Photon each node of the live tree SHALL hold a position, an element, and a `StatefulComponent`
SHALL keep the instance its position first mounted for as long as that position lives. A rebuild that
hands the position a node of the same runtime type and the same `Key` SHALL keep the element and its
instance, and the fresh instance's configuration SHALL reach the kept one through `AdoptConfig`. A
node of another type or another key SHALL replace the element, and its instance SHALL start fresh.
Among the children of a `Row`, a `Column`, a `Stack`, a `Grid` and a `Navigable`, a child with a key
SHALL be matched by its key wherever it moves, and a child without one by its index. A child no old
child can be, because none held its key or the one that held it is of another type, SHALL be made
without being compared with each old child (unless a key repeats among them), so replacing every
member of a list SHALL cost what building the list and letting the old one go cost.

#### Scenario: A parent rebuilds

- **WHEN** a page holds a stateful counter that was pressed three times, and the page rebuilds with a
  fresh counter whose label changed
- **THEN** the counter shows three presses and the new label

#### Scenario: Keyed rows swap

- **WHEN** a column holds two stateful rows keyed `a` and `b`, row `a` was pressed once, and the
  column rebuilds with `b` before `a`
- **THEN** the row keyed `a` still shows one press, now in second place, and `b` shows none

#### Scenario: A key inserted at the front

- **WHEN** a column holds two stateful rows keyed `a` and `b`, pressed once and twice, and the column
  rebuilds with `c` before `a`
- **THEN** the row keyed `c` is a new instance with no presses, and the row keyed `a` is the same
  instance with one press, now in second place

#### Scenario: Every key replaced

- **WHEN** a column of two stateful rows keyed `a` and `b`, both pressed, rebuilds with rows keyed `c`
  and `d`
- **THEN** both rows are new instances with no presses

#### Scenario: Every member of a long list replaced

- **WHEN** a column of 10,000 rows keyed `a0` to `a9999` rebuilds with rows keyed `b0` to `b9999`, or
  with rows of another type keyed `a0` to `a9999`
- **THEN** the rebuild costs less than twenty times what the first build of the column cost, on .NET
  and on the transpiled engine

#### Scenario: The type at a position changes

- **WHEN** a position held a stateful component of one type and the rebuild puts another type there,
  then the first type back
- **THEN** the first type's state starts from its constructor again

### Requirement: What leaves unmounts before what arrives mounts

A live tree SHALL call `OnMount` once for each stateful instance that enters it, after the frame
that built it, and `OnUnmount` once when its position leaves, and within one frame every
`OnUnmount` SHALL run before any `OnMount`. Taking the tree down (`PhotonHost.Unmount`) SHALL unmount
every instance still in it once, the root's included. A tree laid out once, with no host, SHALL call
neither hook.

#### Scenario: Two components swap places

- **WHEN** a frame replaces a mounted component with a component of another type
- **THEN** the leaving one's `OnUnmount` runs before the arriving one's `OnMount`

#### Scenario: A host that keeps drawing

- **WHEN** a host draws a stateful root for a hundred frames and the root calls `SetState` once
- **THEN** the root mounted once, and the host was asked for one frame

#### Scenario: A layout with no host

- **WHEN** a tree with a stateful component is laid out by `LayoutEngine.Layout` alone
- **THEN** neither `OnMount` nor `OnUnmount` runs

### Requirement: A frame builds before it lays out

A Photon frame SHALL build its components before it lays the tree out, and the layout SHALL measure
what was built and build nothing: a component SHALL build at most once in a frame however many times
its parent measures it, and intrinsic sizing SHALL read the same built subtree, so a stateful child is
measured with its own state.

#### Scenario: A container measures a child twice

- **WHEN** a frame lays out a component inside a box whose minimum width makes it measure its child
  again
- **THEN** the component built once in that frame

#### Scenario: A grid sizes a column to its content

- **WHEN** a grid whose column hugs its content holds a stateful component whose width grew with its
  state
- **THEN** the column is as wide as the component's current state draws it

### Requirement: Only what changed builds

A `SetState` SHALL mark its own position for a build and ask the host for a frame, and that frame
SHALL build the marked positions, ancestors before descendants, and nothing else. A frame the host
draws only because something moves (a glide, a transition, a presence, a loop, a frame ticker's
subscriber) SHALL build nothing. A frame after input SHALL build what the input reached: the
positions the handlers it ran marked. Input the host routes into a field, a code surface or a
spreadsheet SHALL be handed to the app's handler. A frame after a key typed into a field SHALL build
what the field's handler marked and nothing else, because the field keeps what was typed itself. A
frame after a code surface's or a spreadsheet's handler that called no `SetState` SHALL rebuild every
component, because the host cannot see which component shows what the surface's model holds. A frame
the host draws for work posted to its thread SHALL build what the work marked. A frame after an
invalidation the host was told of, a resize, a density change or a hot reload SHALL rebuild every
component.

#### Scenario: A loop runs

- **WHEN** a screen of stateful components with a running loop motion draws frames with no input
- **THEN** no component builds in those frames

#### Scenario: One component changes

- **WHEN** one of several stateful components calls `SetState`
- **THEN** the next frame builds that component and what it builds, and no other component

#### Scenario: A press

- **WHEN** a screen holds several stateful cards and a press on one card's button runs a handler that
  calls that card's `SetState`
- **THEN** the next frame builds that card and no other component

#### Scenario: A key typed into a code editor

- **WHEN** a screen holds a code editor beside other stateful components and a key is typed into the
  editor
- **THEN** the next frame builds the editor's component and no component beside it

#### Scenario: A key typed into a field whose handler sets state

- **WHEN** a stateful form holds a field, directly or through a field component with state of its own,
  and the field's handler calls the form's `SetState` with each key
- **THEN** each next frame builds the form and what it builds, and no component above or beside it

#### Scenario: A key typed into a field whose handler sets no state

- **WHEN** a stateful form keeps its field's value without `SetState`, directly or through a field
  component with state of its own, and keys are typed into the field
- **THEN** no component builds in the frames after the keys, and the field shows every key typed

#### Scenario: Work posted to the thread that draws

- **WHEN** work posted from any thread calls one component's `SetState`
- **THEN** the frame it runs in builds that component and no other

### Requirement: A layer builds when it is shown

An `Overlay`'s child and an `Anchored` panel SHALL be built when their layer is realized in a frame,
and a layer that is not realized in a frame (an overlay closed with motion, a panel closed or no
longer hovered) SHALL unmount what it holds.

#### Scenario: A menu closes

- **WHEN** an anchored panel holding a stateful component is open for a frame and closed in the next
- **THEN** the component mounted in the first frame and unmounted in the second

### Requirement: A position keeps its node's state

On Photon the state a node keeps across frames (a scroll view's offset, a drag in progress or gliding
back, what an `InView` last reported, a `Presence`'s entrance, a transition's tracks) SHALL belong to
the node's position and SHALL leave the tree with it. A node that takes a position another node held,
because the type or the key there changed or because the component that built it was replaced, SHALL
start with its own state: a scroll view at its own `Offset`, a draggable at its rest, an `InView`
reporting its first sight, a transition mounting at its value and a `Presence` entering. A node that
keeps its position across a rebuild SHALL keep its state. An `InView` SHALL report only in a tree that
is drawn again, which a host's is and a tree laid out once is not.

#### Scenario: A page replaces another

- **WHEN** a host shows a page whose scroll view was scrolled to 120, and a `SetState` replaces the
  page with a page of another type whose scroll view stands at the same place in the tree
- **THEN** the new page's scroll view is at its own offset, 0

#### Scenario: A key changes

- **WHEN** a box holds a scroll view keyed `a` that was scrolled, and a rebuild hands the box a scroll
  view keyed `b`
- **THEN** the scroll view keyed `b` is at its own offset

#### Scenario: A node keeps its position

- **WHEN** a scroll view was scrolled to 120, and its page rebuilds with a scroll view of the same
  type and key at the same place
- **THEN** the scroll view is still at 120

#### Scenario: A drag does not pass to the next node

- **WHEN** a draggable was dragged 60 and released, and before its glide back ends a rebuild puts a
  draggable with another key at its place
- **THEN** the new draggable is drawn at its rest offset

#### Scenario: An InView at another's place

- **WHEN** an `InView` reported that it is visible, and a `SetState` replaces its page with a page of
  another type whose `InView` stands at the same place and is visible too
- **THEN** the new `InView` reports that it is visible

#### Scenario: A transition at another's place

- **WHEN** a box whose width transitions was 100 wide, and a `SetState` replaces its page with a page
  of another type whose box at the same place is 300 wide and transitions too
- **THEN** the new box is 300 wide in the first frame it is drawn

#### Scenario: A tree laid out once

- **WHEN** a tree holding a visible `InView` is realized once with no owner
- **THEN** the `InView` reports nothing

### Requirement: A presence that leaves fades out from the frame it left in

When a `Presence`'s position leaves the tree, its last paint SHALL be replayed as an exit, paint only,
from the frame it left in until the exit ends, and nothing in it SHALL take input. A `Presence` that
takes the place of one that left SHALL enter while the old one's exit fades. A layer that closes in a
frame SHALL show its presences' exit in that same frame. Presences that leave in one frame SHALL fade
in the order they were painted, whatever order their positions leave the tree in.

#### Scenario: A page with a presence replaces another

- **WHEN** a page holding a `Presence` is replaced by a page of another type holding a `Presence` at
  the same place
- **THEN** the frame that replaces it draws the old presence's last paint over the new one's first,
  and the old one fades out while the new one enters

#### Scenario: An overlay closes with motion

- **WHEN** an overlay with motion that holds a `Presence` closes
- **THEN** the frame that closes it draws the presence's last paint at full opacity, and the frames
  after it fade it out

#### Scenario: Two presences leave together

- **WHEN** a stack holds two overlapping presences, the second painted over the first, and a rebuild
  takes both out, or two layers that each hold a presence close in the same frame
- **THEN** the frame they leave in draws the first one's exit under the second one's

### Requirement: The host finds a control by its position

On Photon the press, the keyboard focus, the field being edited and the hover chain SHALL follow the
position of the node they belong to: they SHALL stay on a control for as long as its position holds
it, across rebuilds, and SHALL NOT pass to a node that takes its place, because a page replaced
another or a key changed. A press whose control left before the release SHALL run nothing. Focus
within a subtree SHALL be the focused control's position lying under the subtree's.

#### Scenario: A page replaces another under the focus

- **WHEN** a button has the keyboard focus and a `SetState` replaces its page with a page of another
  type whose button stands at the same place
- **THEN** no button has the focus, no ring is drawn and Enter presses nothing

#### Scenario: A control replaced during a press

- **WHEN** a pointer is down on a button, and a rebuild puts a button with another key at its place
  before the pointer lifts
- **THEN** lifting the pointer runs neither button's handler

#### Scenario: The same node at a new position

- **WHEN** an app hands one button node to two page types, a pointer is down on it, and one page
  replaces the other before the pointer lifts
- **THEN** lifting the pointer runs nothing, because the button's new position was never pressed

#### Scenario: A hover at another's place

- **WHEN** the pointer rests on a box with a hover style, and a rebuild puts a box with another key at
  its place
- **THEN** the new box is drawn without its hover style until the pointer moves over it

#### Scenario: A keyed row keeps its focus

- **WHEN** a row of a keyed list has the keyboard focus and a rebuild inserts a row before it
- **THEN** the same row keeps the focus

### Requirement: A node's accessibility id is its position's

Each node a platform reader can land on SHALL be named by an integer id that its position carries
from the moment it mounts for as long as it lives, and a linked run inside a paragraph by an id of
its own kept by the paragraph's position. Activating or adjusting through a bridge SHALL name the
node by that id. A node at another's place SHALL have a new id.

#### Scenario: A rebuild keeps the ids

- **WHEN** a screen is read by a platform bridge, rebuilt, and read again
- **THEN** every node the rebuild kept has the id it had

#### Scenario: A reader activates a control

- **WHEN** a bridge activates the id of a button
- **THEN** the button's handler runs, as a press would run it

### Requirement: A shipped component carries every setting a parent hands it

Every `StatefulComponent` the SDK ships SHALL carry, through `AdoptConfig`, every public `set` or `init`
property and every constructor argument of the fresh instance a parent's rebuild hands its position, so the
kept instance answers what the fresh one does. An input read once on purpose SHALL be named, with its
reason, in `tests/eQuantic.UI.Web.Tests/Coverage/adopt-config.baseline.txt`, and that list SHALL only shrink.
A component that keeps the fresh instance whole as its configuration SHALL count as carrying every input.

#### Scenario: A form disables its fields while it submits

- **WHEN** a page holds a `DatePicker`, and rebuilds handing it `Disabled = true`
- **THEN** the picker its position keeps is disabled

#### Scenario: A component drops a setting

- **WHEN** a shipped component's `AdoptConfig` does not carry one of its `init` properties
- **THEN** `AdoptConfigCoverageTests` fails, naming the component and the property

#### Scenario: A setting read once on purpose

- **WHEN** a page rebuilds handing a `CodeEditor` another `InitialCode`
- **THEN** the editor keeps the document someone is typing in, and the baseline names `CodeEditor.InitialCode`
  with its reason

### Requirement: The engine is written in what eqc translates

The element engine (`Element`, `ComponentElement`, `NodeElement`, `BuildOwner`, `ChildSlots` and
`NodeSlots` in `eQuantic.UI.Primitives`) SHALL go through eqc with its `[ServerOnly]` marks lifted with
no error and no warning, and SHALL yield a module for every one of its files. A construct eqc cannot
translate, written into the engine, SHALL fail the build's tests naming its file.

#### Scenario: The engine compiles through eqc

- **WHEN** the engine's sources, their `[ServerOnly]` marks removed, are transpiled by eqc
- **THEN** every file yields a module, and no diagnostic is reported

#### Scenario: A construct eqc refuses

- **WHEN** a call eqc has no translation for is written into one of the engine's files
- **THEN** the test fails, naming the file and eqc's diagnostic

### Requirement: Every node kind has its children's slots

The engine SHALL read the children of every concrete node kind the vocabulary declares. A node whose
kind it does not know SHALL fail with an exception that names the kind, never with an empty answer.

#### Scenario: Every kind is mounted

- **WHEN** one instance of every concrete `VisualNode` in Primitives is mounted through an owner
- **THEN** each is mounted without the unknown-kind exception, apart from the kinds a baseline names
  because no instance can be built for them, and that baseline only shrinks

#### Scenario: A kind is forgotten

- **WHEN** the arm for one node kind is removed
- **THEN** mounting that kind fails with an exception that names it

### Requirement: A component builds through its owner

A component's position SHALL build its component through the owner it belongs to, at the depth of
components the tree reads, and the owner SHALL be handed how a component is built by whoever makes
it. On Photon and on the server that is the error boundary they build through, with its report, its
containment and its depth bound unchanged.

#### Scenario: The owner's build is the one used

- **WHEN** an owner made with a build of its own mounts a page that holds two components
- **THEN** that build is asked for the page at depth 0 and for each component at depth 1, and nothing
  else builds them

#### Scenario: A build that throws is contained

- **WHEN** a component's `Build` throws inside a tree an owner from `ComponentBoundary` builds
- **THEN** the exception is contained and reported as it was before this change

### Requirement: A frame of the tree is lowered on the web from its positions

A `MarkupHost` SHALL lower the tree its `BuildOwner` has just built, between its `Build` and its
`EndFrame`. A component SHALL be lowered from what its position built in that frame, by the
instance the position keeps, never built again. A layer the web lowers (an overlay's child, an
anchored panel, one kept mounted while closed for its motion included) SHALL be shown in its
position's deferred slot before it lowers; a layer the web does not lower SHALL leave the tree when
the frame ends. Of an adaptive node, the arm its element holds SHALL lower from its positions and the
other arms as a tree drawn once does. A component, or a layer's holder, that the frame has no
position for SHALL be refused with an exception. The host's components SHALL build through the error
boundary with a measurer that has no font, and the host SHALL note which build asked it, so a
component whose build asked is marked for hydration as a tree drawn once marks it. The first frame
SHALL be the markup `WebRealizer.Lower` writes for the same tree, byte for byte, hydration's
unmeasured mark included.

#### Scenario: The second frame of a nested stateful component

- **WHEN** a frame is lowered from a page that builds a counter, the counter's lowered click handler
  is invoked, and the next frame is built and lowered
- **THEN** the second frame reads `Count: 1`

#### Scenario: The first frame is the server's markup

- **WHEN** each case of the component parity fixture is built by a live host at `Compact`, `Medium`
  and `Expanded`, and its first frame lowered
- **THEN** each markup equals `WebRealizer.Lower`'s for the same case, the unmeasured mark included,
  and at least one case carries the mark

#### Scenario: A closed panel leaves

- **WHEN** an anchored panel holding a counter is open in one frame and closed, with no motion, in the
  next
- **THEN** the counter's `OnUnmount` runs when the second frame ends

#### Scenario: A component the frame did not build

- **WHEN** a frame is asked for what a component built that no position of it holds
- **THEN** it throws `InvalidOperationException`

#### Scenario: A discovery round names the kept instance

- **WHEN** the server's expansion scope is armed while a frame is lowered in which a page hands a
  stateful child's position a fresh instance
- **THEN** the scope names the instance the position keeps, not the fresh one

### Requirement: An element hands its children to a visitor

`Element.VisitChildren(visitor)` SHALL hand the visitor every child position the element keeps, in
slot order, passing over a slot that holds nothing (a layer not shown yet). It SHALL be the only method
of that name on each kind of element, written so eqc transpiles it without a diagnostic.

#### Scenario: The method crosses to the runtime

- **WHEN** the text of `VisitChildren` in `Element.cs`, `NodeElement.cs` and `ComponentElement.cs` is
  run through eqc in a class holding the same fields
- **THEN** eqc reports nothing, and each kind's module holds one `visitChildren`

### Requirement: A host marks a position to build again

`ComponentElement.MarkNeedsBuild()` SHALL be public, so a host that changed what a component reads
without a `SetState` (a prefetch that loaded its fields) asks for that position alone to build again,
with the instance it keeps.

#### Scenario: A prefetch that loaded

- **WHEN** a stateless component a page builds anew on every build loads its data, its position is
  marked, and the owner builds
- **THEN** only that position builds, with the instance that loaded, and draws what it loaded

### Requirement: A component rebuilds when a value it depends on changes, and nothing else does

A component that reads an inherited value through `DependOn<T>()` SHALL build again in the same frame
when a parent's build hands that value's position a new node whose `UpdateShouldNotify` answers true,
and a component that read nothing of it, or read it once through `Find<T>()`, SHALL NOT. A lookup SHALL
answer the nearest value of its type above the component, and a component whose position left the tree
SHALL NOT build when the value changes. A value inside a layer that changes in the frame the realizer
shows the layer SHALL build its dependents in that frame, before the layer is laid out, and SHALL NOT
ask for a frame after it.

#### Scenario: A value changes under a subtree built once

- **WHEN** a stateful holder places a new value over the same child, which holds a reader, a component
  that reads nothing and one that read the value once
- **THEN** only the reader builds again, in that frame, with the new value

#### Scenario: The value says nothing changed

- **WHEN** the new node's `UpdateShouldNotify` answers false
- **THEN** no dependent builds

#### Scenario: Two values of one type

- **WHEN** a reader stands under two values of its type, with a value of another type between them
- **THEN** it reads the nearest of its type

#### Scenario: A value inside a layer changes in the frame the layer is shown

- **WHEN** a stateful holder builds an overlay whose layer places a new value over the same child, which
  holds a reader and a component that reads nothing, and the realizer shows the layer
- **THEN** the reader builds with the new value before the layer is laid out, the other does not, and no
  position waits for the next frame

#### Scenario: A value changes while a layer's marked positions build

- **WHEN** a stateful holder inside a layer, out of reach of the walk that shows the layer, places a new
  value over the same child, which holds a reader, and builds when the layer is shown
- **THEN** the reader builds behind it in that frame, with the new value, and no position waits for the
  next frame

#### Scenario: A value changes over a layer inside the layer

- **WHEN** the value inside a layer changes over a second overlay whose layer holds the reader, and the
  realizer shows the outer layer and then the inner one
- **THEN** the outer layer does not build the reader, and the inner one builds it with the new value in
  that frame

### Requirement: A stateful component hears a dependency change before it builds

A `StatefulComponent` that depends on a value SHALL have `OnDependenciesChanged()` called before the
build that the value's change caused.

#### Scenario: The value changes

- **WHEN** a stateful dependent's value changes
- **THEN** `OnDependenciesChanged` runs, and then its build reads the new value

### Requirement: A position's context reads the frame and the tree

A component built through an element SHALL build with a context of its position. It SHALL read the
frame's theme, density and type scale from its owner, and a frame that hands a different one SHALL
build again the components that read it and no other. It SHALL answer `InFlow` from whether an `InFlow`
is above its position. A context made with no position SHALL answer `DependOn` and `Find` with null.

#### Scenario: The density changes

- **WHEN** a frame hands a different density
- **THEN** the component that read the density builds again, and a component that read nothing of the
  frame does not

#### Scenario: InFlow

- **WHEN** one component stands under an `InFlow` and another beside it
- **THEN** the first answers true, the second false, and no ambient channel is armed

### Requirement: A component navigates the host of its tree

`Navigator.Of(context).Go(destination)` SHALL hand a non-empty destination to the navigation handler
of the owner of the component's tree, and SHALL do nothing for an empty destination or a context with
no position.

#### Scenario: A component navigates

- **WHEN** a component of a tree whose owner has a handler navigates to `/orders`, then to an empty
  destination, and a context with no position navigates
- **THEN** the handler receives `/orders` once

### Requirement: A component is a node in the browser too

A component, with or without state, SHALL be a node of the vocabulary in the browser as it is in C#:
its kind SHALL be `component` at run time, and it SHALL carry every member the vocabulary's node
declares, a type check failing when the node gains one the component lacks. The DOM escape hatch
(`HtmlElement`) SHALL NOT be one, as in C#.

#### Scenario: A component without state

- **WHEN** a component without state is made in the browser
- **THEN** its kind is `component`, and it is assignable to the vocabulary's node

#### Scenario: The escape hatch

- **WHEN** an `HtmlElement` is made in the browser
- **THEN** it carries no node kind, and the lowering lets it render itself

#### Scenario: The node gains a member

- **WHEN** the vocabulary's node declares a member the component does not
- **THEN** the runtime's type check fails

### Requirement: The engine runs in the browser as it runs on .NET

The element engine SHALL be transpiled into the runtime, its modules committed and pinned byte for byte,
exported from the served runtime and accepted by the runtime's type check, and no hand-written
TypeScript twin of any of it SHALL exist. Driven by the same components through the same steps, the
transpiled engine SHALL answer every case it is held to as the C# engine does: what a position keeps,
what a frame builds and in which order, and what mounts and unmounts.

#### Scenario: A position keeps its stateful instance in the browser too

- **WHEN** an owner of the transpiled engine mounts a stateful component, its state changes, and the
  next build hands the same position a component of the same type and key with new configuration
- **THEN** the same instance holds the new configuration and the old state, as on .NET

#### Scenario: One instance at two positions

- **WHEN** one stateful instance is placed at two positions and they leave one after the other
- **THEN** it unmounts once, when the last leaves, on both engines

#### Scenario: An app member takes a name the engine calls

- **WHEN** an app's stateful component declares a method named `EnterPosition` or `LeavePosition`
- **THEN** the build refuses it with EQ2011

### Requirement: The server draws a write-once page through an owner for the request

The server SHALL draw a write-once page through a structural `MarkupHost` it keeps for the request,
each prefetch round a frame of that one tree, and a client navigation's discovery of it SHALL run
the same rounds through the same kind of host, keeping each component's wire form as it loads. After a frame, `ServerDataRounds` SHALL name the
components to ask: each that loads data, that the frame reached and that was not asked, in the order
the frame named them, save a stateless one below another asked in the same round, which SHALL wait for
the next. A component that loaded SHALL have its positions marked, and only those subtrees SHALL build
in the next frame, with the instance that loaded. A bound of five rounds SHALL count a round that
reached a position, or a component holding none, that no round before it had; past it the page SHALL
be drawn with what it has, its markup and payload agreeing. The markup and the hydration payload SHALL
be byte for byte what drawing the tree once per round wrote: components named `Type#ordinal` in
lowering order, and the payload carrying every component that loads data or holds a value from the
container, read off the instances that drew. An escape-hatch page SHALL keep drawing its write-once
bridges as trees drawn once, putting back by name what each loaded.

#### Scenario: A round draws the instance that loaded

- **WHEN** a page composes a stateless component that loads, and draws whether the instance drawing it
  is the one whose prefetch ran
- **THEN** the markup says it is

#### Scenario: What a round could not put back

- **WHEN** a component's prefetch stores a delegate, and what it builds depends on that delegate and
  loads data of its own
- **THEN** a full load draws what it composed with its data, and a navigation's payload carries it

#### Scenario: A chain deeper than the bound

- **WHEN** each component of a chain exists only once the one above it has loaded, twelve deep
- **THEN** the page is drawn after five rounds, and every link the payload says loaded reads loaded in
  the markup

### Requirement: A render's channels are handed to it

The web SHALL hand each render its channels as one `RenderChannels`: the atomic style sink, the
gradient sink, the face sink, the expansion scope, the density and the link policy. The server SHALL
make one for each prefetch round and one for each discovery, and SHALL hand it to `MarkupHost.Lower`
for a write-once page. An escape-hatch page SHALL be rendered with its round's channels armed by
`RenderChannels.BridgeWith`, and a write-once bridge inside it SHALL read them there, the one web
channel read from the async flow. Every `AsyncLocal` the SDK's source declares SHALL be named in a
list with the reason it is one, and the list SHALL fail on a channel it does not name and on a name
the source no longer declares.

#### Scenario: Two requests drawn at once

- **WHEN** two requests at different densities are drawn at the same time, each held in its page's
  build until the other is drawing too, for a write-once page and for an escape-hatch page
- **THEN** each page carries its own density and its own atomic rules, and none of the other's

#### Scenario: A page in a language

- **WHEN** a page lowered through channels whose link policy is a language's links to `/about`
- **THEN** its href is the language's, and the same page lowered outside a render links to `/about`

#### Scenario: A channel nobody named

- **WHEN** the source declares an `AsyncLocal` the list does not name
- **THEN** the list's test fails, naming it
