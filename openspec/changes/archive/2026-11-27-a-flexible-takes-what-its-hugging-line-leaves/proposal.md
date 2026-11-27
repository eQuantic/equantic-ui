## Why

Closes #1256, a Bug under #1174, found by #996's proof against the real sample.

In the desktop sample's Inputs section, typing into the search field and pressing its clear button left the query in
place, and the quantity stepper in the next column went down instead. Measured through `PhotonHost` at 1180×820:

- **The cell:** the grid's first column is 308dp wide, 255 to 563.
- **The search field:** it was 338dp wide before a key and 396dp after one, 30dp and then 88dp past its cell.
- **The clear button:** it lay at 589 to 637, under the stepper of the next column, which starts at 579. The stepper took
  the press at the button's centre.

**The cause** is the line, not the grid. The field's entry is a `Flexible` between its icon and its clear button, in a
`Row`, in a `Box` that fills a column that hugs its content (the gallery's `Labelled`). A hugging parent leaves the
row's width to its content, so the row has no leftover to share. On such a line a `Flexible` contributes its
max-content size, measured in the room it is offered, and it was offered the line's whole room, 280dp. A text entry
takes what it is offered. So the entry took all 280, and the icon, the gaps and the button were pushed past the line's
end.

**The web** writes a `Flexible` as `min-width: 0`, which shrinks it into the line. Measured in a browser, the same cell
lowered by the web realizer puts the entry at 192dp and the clear button at 246 to 294, inside its 308dp cell.

## What Changes

Nothing an app writes changes. The Studio's search field stays inside its cell, and so does its clear button.

- **A Flexible's max-content probe is offered only what its line has left** once its rigid items and gaps are laid
  (`MeasureVisitor.Flex`), on a line with no leftover to share and a finite room. A short label keeps its own width, as
  before.
- **Several such flexibles give back what they overrun together.** Each gives by its shrink weight times its size, as a
  browser shrinks flex items from their content size (Flexbox §9.7). Offered the same room each, two entries came to
  more than it.

Part reached: the native layout engine's flex measurement. No contract or developer surface moves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `flex-layout`: a Flexible on a line its content sizes takes, at most, what the line has left.

## Impact

- `AFlexibleInAHuggingLineTests` lays out an entry between two rigid items and two entries side by side in a hugging
  column of a grid cell. Both cases fail on main, the first by the issue's 88dp. A short label keeps its width on both
  sides.
- `StudioFieldsTests` pins the sample's computed widths, and presses the clear button at its centre, where main sends the
  press to the stepper.
