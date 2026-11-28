## Why

Closes #1556, a block of the catalog's 1.0 (#191), which waits for every block a complex component needs: a
developer builds any component, at any complexity, in C# with the base blocks alone and no line of JavaScript.
`docs/FLUTTER-PARITY.md` lists `CustomMultiChildLayout` and `CustomSingleChildLayout` as GAPs. Today a layout
the vocabulary does not have (a masonry grid, a radial menu, a timeline whose cards dodge each other) has no
door: `Row`, `Column`, `Grid`, `Stack` and `Positioned` place children by their own rules, and the one node that
reads its room, `LayoutBuilder` (#1555), builds a child for it but cannot measure one.

## What Changes

- **`CustomMultiChildLayout(delegate, children)`**, a vocabulary node, with each child wrapped in
  **`LayoutId(id, child)`**. The **`MultiChildLayoutDelegate`** the app writes in C# measures each child with
  the constraints it chooses (`LayoutChild(id, constraints)` answers the child's `Size`) and places it
  (`PositionChild(id, offset)`), as Flutter's does:
  - `GetSize(constraints)` says the node's size before its children are laid out, the room's biggest by default,
    and `PerformLayout(size)` lays them out, Flutter's two steps;
  - **`Layout(constraints)`** is the one step both run in, and a delegate that sizes itself from its children
    (a masonry grid's height) overrides it instead: it lays the children out and answers the size. Flutter's
    delegate cannot do this, which is why its masonry grids are a package's render object.
  - each child is laid out exactly once, as Flutter asks; `HasChild(id)` says whether a child with that id is
    there, and `ShouldRelayout(old)` says whether a new delegate lays out otherwise than the one it replaces.
- **`CustomSingleChildLayout(delegate, child)`**, with a **`SingleChildLayoutDelegate`**: `GetSize`,
  `GetConstraintsForChild` and `GetPositionForChild`, as Flutter's.
- **Both targets run the app's delegate.** Photon runs it in its layout pass, where Flutter does. The browser
  runs the transpiled delegate right after its own layout, as it runs a `LayoutBuilder`: it measures the
  node's room, measures each child under the constraints the delegate asks for, and places it. The server
  writes the children, so their content and their server data are in the page, and the browser places them.
- **The node takes its room as a box that fills**, as `LayoutBuilder` does: the delegate is told the size the
  parent decides on each axis it decides, and an unbounded maximum where the size is left to what it holds.
  The node's size is the delegate's on an axis sized by content.
- **The reading order is the children's order**, wherever the delegate places them, on every target.
- **Email refuses the node**, as it refuses a `LayoutBuilder`: a message has no layout pass.

What a developer writes:

```csharp
sealed class Radial(int count, float radius) : MultiChildLayoutDelegate
{
    public override Size GetSize(BoxConstraints constraints) => new(radius * 2 + 48, radius * 2 + 48);

    public override void PerformLayout(Size size)
    {
        var centre = new Point(size.Width / 2, size.Height / 2);
        for (var i = 0; i < count; i++)
        {
            var child = LayoutChild(i, BoxConstraints.Loose(new Size(48, 48)));
            var angle = 2 * MathF.PI * i / count;
            PositionChild(i, new Point(centre.X + radius * MathF.Cos(angle) - child.Width / 2,
                centre.Y + radius * MathF.Sin(angle) - child.Height / 2));
        }
    }
}

CustomMultiChildLayout(new Radial(actions.Count, 96),
    children: [.. actions.Select((action, i) => LayoutId(i, IconButton(action.Icon, action.Label, onPressed: action.Run)))])
```

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `layout`: a node lays its children out with a delegate written in C#, on Photon and in the browser.

## Impact

- **Vocabulary** (`eQuantic.UI.Primitives`): `CustomMultiChildLayout`, `LayoutId`, `MultiChildLayoutDelegate`,
  `CustomSingleChildLayout`, `SingleChildLayoutDelegate`, their visitor methods and node kinds. The public
  surface grows; nothing goes.
- **Photon** (`Native.Framework`, `Native.Components`): a measure door that runs the delegate, and the emit,
  hit-testing and semantics walks that follow the places it chose.
- **Runtime and web realizer**: the twins of the nodes and of the two delegate base classes, the lowering on
  the server and in the browser, and the measurement after the pass beside the builders'.
- **eqc**: an app's delegate is a class that extends a vocabulary class, transpiled as any other class; its
  base is the runtime's twin. Whatever the translation lacks is a defect this change fixes.
- **Email**: refuses the two nodes.
- **Samples and docs**: the dashboard's layout page lays out a masonry grid and a radial menu; the Photon
  desktop gallery shows the same; `docs/FLUTTER-PARITY.md`'s two rows read SAME; the wiki's layout page in its
  five languages; a ledger entry.

No break: everything is new.

## Principles

- **1, never a platform artifact**: a layout the vocabulary lacks is C#, never a stylesheet or a script.
- **4, written once**: the delegate is the app's one class, run by both targets.
- **6, Flutter**: `CustomMultiChildLayout`, `LayoutId` and the two delegates keep Flutter's names and steps;
  the one widening, a delegate that sizes itself from its children, is what Flutter's community builds with a
  render object (`boxy`'s `BoxyDelegate`), and the spec states it as ours.
- **3, the bar is the transpiler**: the delegate runs in the browser as transpiled C#, and a construct it uses
  that eqc cannot translate is fixed there, never worked around in the sample.
