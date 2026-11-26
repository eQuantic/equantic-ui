# Proposal

Closes #1344, a Bug under #1587.

## Why

Photon drew a box's fill with antialiased edges wherever they fell, a fractional device pixel included. Two layers that share an edge there each covered the same fraction of that pixel. The one behind blended in first and the one in front blended over it, so part of the layer behind survived as a hairline. A `SwipeableRow` keeps its action behind the row's surface, the same height. On the Android emulator, at 2.625 device pixels a dp, a 78 dp row ends at 204.75 px, and a closed row showed a red line under it (found proving #1343). Browsers snap a background's edges to device pixels, so the web draws no line for the same tree.

## What Changes

- **A fill's edges land on whole device pixels.** `DisplayListBuilder.FillRRect` moves each edge of an axis-aligned fill to the nearest whole device pixel under the current transform, half a pixel rounding up, as a browser snaps a box's background. Two layers that share an edge then each cover the pixel or none of it.
- **A clip's edges land on whole device pixels too.** `PushClip` snaps the clip it bakes in device space when the transform keeps it axis-aligned, as a browser snaps an overflow clip. A swipeable row draws its action and its surface under the clip its own box keeps them in, and that clip ended on the same fractional pixel. Snapping the fills alone still took a quarter off both layers in that pixel row, so the emulator still showed the red line.
- **What it leaves alone:**
  - a fill under a transform that turns or skews it, which no pixel grid lines up with;
  - a disc (`FillCircle`), which would come out a pixel out of round;
  - the corners, which keep their antialiasing as a browser's do.
- **A fill thinner than a pixel keeps one**, so a hairline never vanishes.
- **This is option A, every fill**, chosen over snapping only the edges two fills share: it is the browser's own rule, and a near-coincident seam still bleeds under the narrower one.

The public surface does not move. Goldens move by their edge pixels only.

## Impact

- `eQuantic.UI.Native.Engine`: `DisplayListBuilder.FillRRect`, `FillCircle` and `PushClip`.
- 76 goldens of the engine suite, regenerated after an audit of each diff.
- Six tests that read a fractional fill.
- `LayerSeamTests`, new: two plain layers, and the real `SwipeableRow`.
