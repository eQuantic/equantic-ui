## Context

`docs/FLUTTER-PARITY.md` lists `LayoutBuilder` as a GAP and `BoxConstraints` as DIFFERENT: the constraint the
layout hands down exists, as `LayoutConstraints` in `Native.Framework`, but it is the engine's record. Its
`Indeterminate`, `Stretch`, `Truncating`, `WidthIsACeiling` and `Visible` are the engine's policy, and the
web, whose layout is the browser's, could produce none of them. `docs/ELEMENT-TREE-PLAN.md` 3.2 already says
how the builder should arrive: layout measures what the elements built and builds nothing, and "the one
consumer that will (`LayoutBuilder`) gets a build in layout as an explicit element kind later, as Flutter's
does, not as the rule for every component".

## Goals / Non-Goals

**Goals:** a value an author receives, holds, computes with and applies; a node that builds its child for the
room its parent gives, on Photon and on the web, answering the same constraints for the same tree at the same
window; the canvas ruler retired.

**Non-goals:** a custom layout delegate (#1556), slivers (#1557), and the min-content and max-content
intrinsics of what a builder has not built yet.

## Decisions

### How Flutter does it

`LayoutBuilder(builder: (context, constraints) => …)` builds during layout. Its element runs the builder from
`performLayout` with the `BoxConstraints` its render object was given, and only when they changed or the
element was marked dirty. The child is laid out with the same constraints, and the builder's size is the
child's. `BoxConstraints` is four numbers, and unbounded is an infinite maximum.

### The value is Flutter's, and the engine's record stays the engine's

`BoxConstraints` goes in `Primitives`, beside `Size`, `Rect` and `EdgeInsets`, with Flutter's shape and the
members an author reaches for. Its default constructor is unbounded (0 to infinity on both axes), as
Flutter's is. The engine's `LayoutConstraints` keeps its flags and its name: it is what Photon measures with,
and the author's value is projected from it in one place. Moving the engine's record down would have put its
flags in the vocabulary, where the web can answer none of them.

The twin in `value-types.ts` rounds every number to single precision. Its arithmetic is cross-pinned with the
existing fixture: `PrimitiveValueFixtureTests` computes the answers in C#, and `primitives-exports.spec.ts`
asserts the twin's against them, as `Rect`'s already are.

### What room the builder is told

The two targets have no layout pass in common, so the room has to be something both can produce. The node
takes its room as a `Box` whose width and height `Fill` does. Box parity is already pinned on both targets, so
the node inherits it rather than adding a new sizing rule. On each axis:

- where the parent decides the box's size (a bounded axis that is not sized by content, or one the parent
  stretches), the builder is told that size as the maximum;
- where the box's size is left to what it holds (an unbounded axis, or one a hugging parent sizes from
  content), the builder is told an unbounded maximum.

The minimums are zero, so the builder is told how much room there is, and how the child takes it is the
child's own `Width` and `Height`. Flutter's builder can also be handed a tight constraint. Ours never is,
because a filling box already decides its size on every axis the parent decides. The canvas ruler was a
`Canvas` with `Width` and `Height` at `Fill`, so the node measures what that ruler measured.

### Photon: a build in layout, through a slot the realizer shows

`ChildSlots` gives the node one deferred slot, as an overlay's child has, so the element tree never builds
it in the build phase. The measure door does five things, in order:

1. projects `BoxConstraints` from the engine's constraints, by the rule above;
2. asks the builder for the child, unless the node and the constraints are the ones it last built for. The
   last child it built is kept on the position's `State`, beside the frame it was last measured in;
3. shows that child in the slot (`NodeElement.ShowDeferred`), which builds its components and the dirty
   positions under it;
4. measures the child as a filling box's child is measured, through `MeasureBox`, which now takes the box's
   style and child as arguments;
5. shows the slot every frame, built again or not, because a slot not shown in a frame is taken out when the
   frame ends.

A container that measures the node twice in one frame under different constraints (a box whose minimum or
maximum bites measures its child again at the size it settled on) builds for both the first time, as Flutter
would. Flutter keeps a layout until something changes, though, and Photon lays out in every frame, so the
pair would build two children in every frame and the slot would take one's components out and put the
other's in, twice a frame. So when a later measurement of a frame builds another child, the frame's first
room is kept beside the room that child was built for, and the next frame's first measurement in that first
room shows the settled child. A later measurement of a frame in that room is the one the frame settles on,
and gets its own child. Each room is built once, and the child stays from the second frame on.
Intrinsic sizing reads the child the slot holds, and nothing before its first build. Every place the engine
asks whether a child fills (`FillsOf`, `DeclaredSizeKind`, the fill height of an intrinsic) answers for the
node as it answers for a `Box` with `Fill` on both axes. Emit and semantics pass through.

### Web: built after the browser's layout, against the same room

The browser lays out after the runtime writes the DOM, so a builder cannot run inside that layout. It runs
after it, which is what `docs` already said a web `LayoutBuilder` would be ("a measurement read after the frame
and built back"):

- **The server** writes the filling box with nothing in it and no marker. It has no box to measure, and a
  guess would draw a child the browser then replaces. The components under the builder are built only in
  the browser, so they get no server data. A decision that has to render on the server is a window size
  class, which `AdaptiveNode` serves.
- **The browser's first lowering** has no measurement either and writes the same empty box, so hydration
  adopts it. The node's state lives on its position (`node-states.ts`). The lowering marks the box as a
  builder to measure, and the pass's commit (`exitPass`) measures every marked box.
- **The measurement** reads the box as if it held nothing: `contain: size` is set on it for two reads of
  `getBoundingClientRect` and taken off. Under size containment, an axis the parent decides keeps its size,
  and an axis sized by content takes the box's `contain-intrinsic-size`. The two reads set two intrinsic
  sizes: an axis that follows them is sized by content and read as unbounded, and one that does not is the
  parent's, zero included, so a box decided at zero is told zero, as Photon tells it. A `ResizeObserver` measures again
  whenever the box changes size. A change of content changes only an axis sized by content, and that axis
  measures unbounded again, so content never feeds back into the constraints.
- **When the measured constraints change**, the nearest component above the node is marked to build. The
  builder is that component's closure, so the render that follows is a patch of that component (#1500), and
  its lowering finds the measured constraints on the position, asks the builder, and shows the child in the
  slot, as an overlay's layer is shown.

The web depends on #1575. Before its fix, a component that built alone ended a frame in which the deferred
slots elsewhere on the page were not shown, and the owner took their components out while their DOM stayed.
Every builder on a page would have lost its components whenever another component built.

### `ConstrainedBox` is a component over `Box`

A `Box` already takes a minimum and a maximum on each axis, and both targets agree on how it does, so the
constraint an author holds is applied by a component that builds a `Box` with those bounds and no other
style. A finite maximum becomes `MaxWidth` and `MaxHeight`, and a minimum becomes `MinWidth` and `MinHeight`.
Its parity is the `Box`'s, with no new realizer code. Our targets follow CSS, where a stated maximum wins over
a parent's stretch, so a `ConstrainedBox` narrower than a slot that stretches is its maximum wide, not the
slot's width as Flutter's `enforce` would make it. That is the existing `Box` rule, and the spec says so.

### Email refuses the builder

A message has no box to measure. The email walk refuses the node as it refuses a canvas, with the one reason
both alternatives share.

## Risks / Trade-offs

- **The first paint of a builder's child on the web comes one render after hydration.** A filling canvas pays
  the same today. The alternative, a guessed constraint on the server, draws content the browser replaces,
  which is worse than drawing nothing for a frame.
- **No server data under a builder.** This is stated in the docs, with `AdaptiveNode` named for what has to
  render on the server.
- **A measurement forces one synchronous layout per marked box**, when the box first appears and each time it
  resizes. That is the cost a `ResizeObserver` user pays anyway.
