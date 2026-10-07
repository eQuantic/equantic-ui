## Context

Photon realizes a frame into a display list and a set of input regions, collected through one
`InputSink` that already carries the clip of the scroll views around a subtree. The host
(`PhotonHost`) answers a press, a hover, the cursor, a drag and a scroll from those regions. Each of
the five defects is a region, or a layout, that disagrees with what is drawn.

## How Flutter answers it

- **Neighbouring targets (#630).** Flutter grows a control's target in LAYOUT
  (`MaterialTapTargetSize.padded`), so two padded targets never overlap. Photon grows it without
  moving anything, to keep the design's geometry (§08), so targets do overlap, and the host has to
  decide. The rule it takes is the one a padded target gives in Flutter: a point inside a control's
  own box is that control's; a slop answers where no box beside it is drawn, and in front of the box
  it stands in, the way a padded `IconButton` owns the padding inside its card.
- **Transforms (#513).** `Transform` hit-tests its child through the inverse matrix
  (`transformHitTests`, true by default), and a `GestureDetector` defers to its child: it is hit
  where its child is drawn. CSS hit-tests the transformed element too.
- **The border (#629).** `Container` insets its child by its decoration's border, as CSS does with
  `box-sizing: border-box`.
- **Text (#285).** Flutter measures a space with the font's advance, on every path.

## Decisions

- **One sink, every region kind.** The sink that clips a subtree's regions also places them: under a
  transform it registers each region at the box its rect is drawn in. That box is exact for a
  translation, a scale and a quarter turn; for a rotation or a shear the frame keeps, in a side table
  created only when something interactive is transformed, the inverse matrix and the local rect, and
  the host tests a point against the shape. A frame that transforms nothing interactive pays nothing.
- **A hit region carries the box it is drawn in** beside the target its slop grows. The host takes the
  topmost region drawn under the point, unless a region in front of it reaches the point with its slop
  and stands inside that box. The cursor, a tap and a press share that one resolution.
- **The frame pays for it.** The 16 bytes a region gained are paid for by sizing each of the frame's
  region lists like the frame before's: a list grown from empty by doubling allocated near three
  times what it held. The pooled steady frame measured 71.2 KB with both, from 73.2, and the ratchet
  comes down to 72 KB.
- **The visible part of a field and a sheet** is carried beside their whole bounds, as a code
  surface's has been since #297: the whole bounds place the caret and the cells, the visible part
  takes the press.
- **A box's insets** are its padding and its border on the sides it draws, one helper read by the
  measure, the reflow and the min-content width.
- **The stand-in charges every space**: between two words, at the paragraph's ends, and a run that is
  only a space. A break drops the spaces it falls on. It stands in for the shells' measurers, and
  CoreText's typographic width counts a trailing space where CSS hangs it, so a plain text that ends
  in a space measures that space on Photon and not on the web.

## Fences

- An editing surface under a scale or a rotation takes the press in the right place and still turns
  it into a caret or a cell through its layout rect. Reported on its own (#658) rather than widened
  into this change.
- A region a `Draggable` moves, mid-drag, stays at its layout rect: a tap there is cancelled by the
  slop rule before it lands, as the emit's own comment says.
- A clip under a rotation is the box around its corners: a scroll view inside a rotated box admits a
  press in the corners of that box, outside its drawn viewport. Nothing in the SDK rotates a scroll
  view, and a clip of any shape is a change of its own.
- A state's `BorderWidth` (a focus ring drawn as a thicker border) draws over the content on Photon,
  which keeps the base border's inset, where the web reflows the content inside it. The layout reads
  the box's declared style, as it does for every member a state changes.
