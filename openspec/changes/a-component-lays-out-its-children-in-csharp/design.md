## Context

The vocabulary places children by fixed rules: flex (`Row`, `Column`), tracks (`Grid`), layers (`Stack`,
`Positioned`). #1555 gave the author the room a box has (`BoxConstraints`, `LayoutBuilder`), but a builder
answers a child and measures none. A layout the rules lack, a masonry grid or a radial menu, has no door, and
`docs/FLUTTER-PARITY.md` lists the two nodes that are Flutter's door as GAPs. The browser and Photon share no
layout pass, so whatever runs the app's layout has to run in both: in Photon's pass, and after the browser's.

## How Flutter solves it

`CustomMultiChildLayout(delegate:, children:)` takes children wrapped in `LayoutId(id:, child:)`. Its render
object asks the `MultiChildLayoutDelegate` for its size first (`getSize(constraints)`, the biggest by default),
constrains it, and then calls `performLayout(size)`, where the delegate calls `layoutChild(id, constraints)`,
which lays the child out and answers its `Size`, and `positionChild(id, offset)`. Each child must be laid out
exactly once; `hasChild(id)` lets a delegate skip a missing one; `shouldRelayout(old)` says whether a new
delegate lays out otherwise. `CustomSingleChildLayout` is the one-child shape: `getSize`,
`getConstraintsForChild`, `getPositionForChild(size, childSize)`. The size cannot depend on the children, so a
masonry grid in Flutter is a package's render object (`flutter_staggered_grid_view`), or `boxy`'s
`BoxyDelegate`, whose `layout()` lays the children out and then answers the size.

## Decisions

### 1. Flutter's nodes and delegates, with Flutter's names and steps

`CustomMultiChildLayout`, `LayoutId`, `MultiChildLayoutDelegate`, `CustomSingleChildLayout` and
`SingleChildLayoutDelegate` go in `Primitives`, beside `LayoutBuilder`. The delegates are abstract classes the
app extends, as Flutter's are, because the delegate holds the app's state (a column count, the items' order) and
its two steps are methods, not a record of closures. The ids are `object`, compared by `Equals`, as Flutter
compares them by `==`; the browser's twin compares them by the runtime's value equality, so an `int`, a `string`,
an enum or a record id means the same on both targets.

### 2. One widening: a delegate may size itself from its children

`Layout(constraints)` is the one step both targets call. Its default is Flutter's: `GetSize(constraints)`,
constrained, then `PerformLayout(size)`, and it answers the size. A delegate whose size follows its children
overrides `Layout` instead, lays them out with the same `LayoutChild` and `PositionChild`, and answers the
size it settled on. This is `boxy`'s shape, the one Flutter's own community reaches for, and it is what the
masonry sample needs. The parity row says SAME, and says this is wider.

### 3. The room is a filling box's, as a LayoutBuilder's

The node takes its room as a `Box` that fills both axes, exactly as `LayoutBuilder` does (#1555): the delegate is
told the size the parent decides on each axis it decides, and an unbounded maximum where the size is left to what
the box holds. Photon projects it with `FillingRoom`; the browser reads it with `roomOf`, under size containment.
On an axis the parent decides, the node is that size. On an axis sized by content, the node is the size `Layout`
answered, which must be finite there: a delegate that answers an infinite size on an unbounded axis is refused
with a message naming the delegate, where Flutter's render object asserts.

### 4. A child is measured as a box with those bounds measures it

`LayoutChild(id, constraints)` lays the child out as the content of a box whose minimum and maximum are the
constraints: a hugging child is its own size inside them, a child that fills takes the maximum on that axis, and a
tight constraint is the child's exact size. That is the `Box` rule both targets already agree on (box parity,
`ConstrainedBox` in #1555), so the children inherit it rather than a new sizing rule. Photon measures the child
with the `LayoutConstraints` the bounds project to; the browser sets the bounds on the child's wrapper and reads
its box. A child laid out twice in one layout, a duplicated id or an unknown one is refused, as Flutter asserts;
a child the delegate never lays out is not drawn and takes no room.

### 5. Photon runs the delegate in its layout pass

A measure door projects the room, binds the delegate to the node's children for one call of `Layout`, measures
each child `LayoutChild` asks for and records the offset `PositionChild` gives. The children's `LayoutNode`
bounds are those offsets, so the emit, hit-testing and semantics walks follow them with no new code. Photon lays
out every frame, and the delegate runs every frame; `ShouldRelayout` is honoured where a layout is kept, which on
Photon is nowhere yet.

### 6. The browser runs the transpiled delegate after its layout

The app's delegate is C# that eqc transpiles like any class; its base is the runtime's twin of the vocabulary's
delegate, which binds `layoutChild`, `positionChild` and `hasChild` to the browser for one call of `layout`.

- **The server** writes the node's box, and each child inside a wrapper placed at the box's origin, the box hidden
  until the browser places them. The children's content and their server data are in the page, so hydration adopts
  them; the first paint of the region waits one render, the cost a `LayoutBuilder` already pays.
- **The pass's commit** measures the node's room as a builder's is measured, runs `layout`, sets each wrapper's
  bounds before reading its box, writes each child's offset as a translation, sizes the box on its content-sized
  axes and reveals it.
- **A `ResizeObserver`** on the box and on every child runs the layout again when the room or a child's size
  changes. A rebuild hands a new delegate; the layout runs again when `ShouldRelayout(old)` says so or the
  children changed. Nothing is built: a layout writes styles only.

### 7. The reading order is the children's order

A delegate places, it does not reorder: the semantics walk on Photon and the DOM order on the web are the
children's order, whatever the offsets. A layout whose visual order is not its reading order is the app's to
order in its children list.

### 8. Email refuses both nodes

A message has no layout pass and no script. The email walk refuses the nodes, as it refuses a `LayoutBuilder`.

## Risks / Trade-offs

- **The first paint of the region waits one render in the browser.** The alternative, children in flow until the
  browser moves them, shifts the page under the reader, which is worse.
- **A delegate is the app's code running in layout**, every frame on Photon and on every resize in the browser. A
  slow delegate is a slow layout, as in Flutter. The sample's delegates are linear in their children.
- **The transpiler meets an app class extending a vocabulary class with protected members**, which no app type did
  before. Whatever it lacks is fixed in eqc, with a conformance case executed on both sides.
