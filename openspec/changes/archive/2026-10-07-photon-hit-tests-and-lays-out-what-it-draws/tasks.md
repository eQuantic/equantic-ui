# Tasks

## 1. A neighbour's slop (#630)

- [x] 1.1 `HitRegion.Drawn`, the box a pressable is drawn in, clipped like its target
- [x] 1.2 The host's one resolution for a press, a tap and the pointer's shape: the box drawn under the
      point, unless a slop in front of it stands inside it
- [x] 1.3 The frame's region lists sized like the frame before's, and the pooled ceiling down to 72 KB
- [x] 1.4 Check: rows under a finger and a pointer, a disabled neighbour, a control in a card, and the
      completion list's rows under a finger, each proved failing without the fix

## 2. Transforms (#513)

- [x] 2.1 The sink places every region kind where a transform draws it, and keeps the inverse of a
      tilting one in a side table the frame creates only when it needs it
- [x] 2.2 The host tests a point against a transformed region's shape, at every place it reads a region
- [x] 2.3 A pressable that wraps a single box takes the pointer where that box is drawn
- [x] 2.4 Check: a translated, a scaled and a rotated square pressed and hovered at their drawn edges,
      proved failing without the fix, and the pooled frame measured

## 3. Editing surfaces past their view (#635)

- [x] 3.1 `TextRegion.Visible` and `SheetRegion.Visible`, the part on screen, and the host's presses and
      pointer through them
- [x] 3.2 Check: a field and a sheet past their scroll view, proved failing without the fix

## 4. The border (#629)

- [x] 4.1 A box's insets are its padding and its border on the sides it draws, in the measure, the
      reflow and the min-content width
- [x] 4.2 Check: a border on every side and on one side, and the goldens that move read one by one and
      regenerated with what moved

## 5. Spaces (#285)

- [x] 5.1 The stand-in charges every space its advance, and a break drops the spaces it falls on
- [x] 5.2 Check: a rich paragraph measures as its plain twin, the space against CoreText's on a Mac, and
      the goldens that move read and regenerated

## 6. Copilot's first round

- [x] 6.1 A transform that collapses a box leaves its regions no area
- [x] 6.2 A box with no area encloses nothing, so a row clipped away whole wins no press
- [x] 6.3 The rich paragraph holds the spaces after a word until the next word or a break
- [x] 6.4 Check: a collapsed square pressed and hovered, a press low in the last row a scroll view
      shows, and rich paragraphs that break and open with spaces, each proved failing without its fix

## 7. Documentation

- [x] 7.1 The wiki's Photon and Styling pages, in English and Portuguese
- [x] 7.2 One `docs/LEDGER.md` line citing the five issues
- [x] 7.3 Archive this change before the merge
