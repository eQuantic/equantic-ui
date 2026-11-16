## Why

Closes #1555, a slice of #191 (the layout protocol: LayoutBuilder, authorable constraints, custom layout
delegates, a sliver contract), part of the block catalog 1.0 waits for: every block a complex component needs,
built in C# alone.

A component cannot ask how much room it has. The first external consumer of this SDK needed a child built
against its own box, found no way to ask, and measured with a `Canvas` whose paint callback does nothing, the
one node that is told its own `Size`. The window's width is all the vocabulary answers (`AdaptiveNode`), and a
card three columns wide in a wide window is as narrow as a phone. The constraint the layout hands down is no
help either: `LayoutConstraints` is the Photon engine's own record, in `Native.Framework`, carrying the
engine's flags, and no author can receive it, hold one, or hand one to a node.

## What Changes

- **Vocabulary** (`eQuantic.UI.Primitives`), all added:
  - `BoxConstraints`: the room a box may take, as Flutter's value has it. A minimum and a maximum on each
    axis, an unbounded maximum being `float.PositiveInfinity`. `Tight(size)`, `Loose(size)`, `IsTight`,
    `HasBoundedWidth`, `HasBoundedHeight`, `Biggest`, `Smallest`, `Constrain(size)`, `Enforce(other)`,
    `Loosen()` and `Deflate(insets)`.
  - `LayoutBuilder(builder)`: a node whose child is what `builder` answers for the `BoxConstraints` its
    parent gives it. It takes its room as a `Box` whose width and height `Fill` does, and the builder is
    handed that room as the maximum on each axis the parent decides, and an unbounded maximum on an axis its
    size is left to what it holds. The minimums are zero.
- **Components** (`eQuantic.UI.Components`): `ConstrainedBox(constraints, child)`, the box that applies a
  `BoxConstraints` an author holds: its child is given the room its parent gives, at most the maximums, and the
  box is at least the minimums. The `LayoutBuilder` and `ConstrainedBox` factories in `UI`.
- **Photon**: the builder runs in the layout pass, as Flutter's does. The node's position holds the child it
  built in a slot the realizer shows, and the builder runs again only when the constraints or the node change.
- **Web realizer**: the server writes the box empty, since it has no box to measure. In the browser, the
  box is measured after the page is drawn, as if it held nothing, which is the room its parent gives. The
  child is then built for it, and built again when the room changes, through the same deferred slot.
- **Email**: a `LayoutBuilder` is refused, as any node that needs a measured box is.

What a developer writes, a card that lays its fields side by side when it has the room:

```csharp
LayoutBuilder(constraints => constraints.MaxWidth >= 480
    ? Row(gap: Space.S4, children: [Flexible(Amount()), Flexible(Category())])
    : Column(gap: Space.S3, children: [Amount(), Category()]))
```

And a box that holds an author's own constraints:

```csharp
ConstrainedBox(BoxConstraints.Loose(new Size(320, 480)), child: Preview())
```

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `layout`: a child built against the room its parent gives, the constraints an author holds and applies, and
  what the server writes before it has a box.

## Impact

- **Vocabulary**: `Layout/BoxConstraints.cs`, `Nodes/LayoutBuilder.cs`, `IVisualNodeVisitor` (one visit),
  `Elements/ChildSlots.cs` (a deferred slot). `PublicAPI.Unshipped.txt`, additions only.
- **Components**: `ConstrainedBox.cs`, `UI.cs` (two factories). Additions only.
- **Photon** (`Native.Framework`, `Native.Components`): the measure door, the intrinsics and the fill reading;
  emit and semantics pass through. No public surface moves.
- **Web realizer** (C#): the empty box on the server. **Runtime** (TypeScript): the `BoxConstraints` twin, the
  node's twin, its lowering, the measurement after the pass, the regenerated node kinds and transpiled modules.
- **Email realizer**: the refusal.
- **eqc, the SDKs, the templates, the shells**: untouched. The developer surface does not move.
- **Docs**: `docs/FLUTTER-PARITY.md`'s `LayoutBuilder` and `BoxConstraints` rows read SAME; the wiki's Layout
  page in every language it is written in; a ledger entry.
- **Depends on #1575**: on the web, a component that builds alone used to unmount the components of a deferred
  slot elsewhere on the page. The web half lands on its fix.

No break: everything is added.

## Principles

- **4, written once, realized per target**: one node and one value, measured in Photon's pass and in the
  browser's, pinned against each other.
- **5, the vocabulary speaks no target's language**: `BoxConstraints`, `LayoutBuilder` and `ConstrainedBox`
  are Flutter's names, which the vocabulary takes first, and none of them is a CSS word.
- **6, ask how Flutter solves it**: Flutter's `LayoutBuilder` builds in layout with the parent's
  `BoxConstraints`; Photon does exactly that. The web cannot build inside the browser's layout, so it builds
  after it, against the same room.
- **9, the structural fix**: the builder is an element kind with a slot the realizer fills, the mechanism the
  overlays already use, rather than a component measuring itself through a canvas.
