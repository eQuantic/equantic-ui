## Context

Since #513 the frame keeps, for every region registered under a transform, the inverse matrix and
the rect the region was laid out at (`TransformedRegion`, in a side table created only when something
interactive is transformed), and the host tests a point against the region through it. The
conversions that come after the hit, a point into a caret, a code position, a cell or a canvas's
coordinates and a caret back onto the screen, still subtracted or added the corner of the box the
region is drawn in.

## How Flutter answers it

- A `PointerEvent` reaches a render object with its `localPosition`, the point taken back through
  every transform between the screen and that object (`PointerEvent.transformed`), and
  `EditableText` places the caret from it: a press means the same on a scaled field as on a plain one.
- The engine places the input method's candidate window from the editable's size and its transform
  (`TextInputConnection.setEditableSizeAndTransform`).
- `RenderViewport.getOffsetToReveal` measures its target in the viewport's own space
  (`getTransformTo`), so a scaled viewport reveals by layout distances.

`docs/FLUTTER-PARITY.md`'s `Transform` row stays PARTIAL for what it lists (two dimensions, a pivot
fixed at the centre); its note gains that input reads a transform too.

## Decisions

- **Two conversions over one table.** `ToLocal` takes a point on screen into a region's own space,
  relative to its corner, through the inverse the frame already keeps; `ToScreen` places a rect from
  that space on screen. Every place the host read a region's corner goes through one of them: a
  field's caret on a press and a drag, a code surface's press, drag and release, a sheet's press,
  drag, fill handle and fill drag, a canvas's pointer, the candidate window's anchor and a code
  caret's reveal. A region no transform drew takes the subtraction it always did.
- **The forward matrix is not kept.** The table holds the inverse, which every pointer move asks
  for. `ToScreen` needs the forward one only for the candidate window and a reveal, so it inverts the
  inverse back rather than growing every transformed region by a matrix.
- **The fill handle is compared in the sheet's own space**, so its grab distance scales with the cell
  it is drawn on, as the handle does.
- **A canvas is found in the current frame by its path**, as a code surface and a sheet are: the
  region a drag began on is a frame old, and its transform with it.
- **What a code surface offers is tested against its own shape.** The side table keeps the list's
  rect in the surface's space beside the surface's own, and the pointer's shape and a press read the
  list through the inverse, as they read the surface.
- **A reveal measures in the scroll view's own space** (`InScrollSpace`): the control or the caret and
  the viewport are both taken back through the scroll view's inverse, the margins with them, so a
  scroll view drawn twice as large scrolls exactly as far as it would unscaled.
- `InputSink.Place` reuses `Matrix2D.TransformBounds`, which it had copied.

## Fences

- A scroll view inside a rotated box reveals along its own axis, and the clip it registers is still
  the box around its corners (the #513 fence).
- The editor model's release ignores the point it is given, so nothing observable proves the
  release's conversion: it goes through `ToLocal` like the press and the drag, for a model that
  reads it.
