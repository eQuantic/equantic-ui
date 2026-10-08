# Tasks

## 1. A press on a transformed surface (#658)

- [x] 1.1 `ToLocal`: a point on screen in a region's own space, through the frame's inverse when a
      transform drew the region
- [x] 1.2 A field's caret on a press and a drag, a code surface's press, drag and release, a sheet's
      press, drag, fill handle and fill drag, and a canvas's pointer, all through it
- [x] 1.3 Check: each under a 2× scale and a sheet under a quarter turn, failing on the code before the
      fix, and each drag site reverted alone
- [x] 1.4 What a code surface offers, tested against its own shape for the pointer's shape and a press,
      and checked with a list turned 30°, failing without the fix

## 2. The other way (#700)

- [x] 2.1 `ToScreen`, and the candidate window's anchor for a field and a code surface through it
- [x] 2.2 `InScrollSpace`: both reveals measure in the scroll view's own space
- [x] 2.3 Check: the anchor under a 2× scale, and a scroll view drawn twice as large scrolling to a
      control Tab reached and following a caret exactly as far as unscaled, each reveal site reverted
      alone

## 3. Documentation

- [x] 3.1 The wiki's Photon page, in English and Portuguese
- [x] 3.2 One `docs/LEDGER.md` line citing both issues
- [x] 3.3 Archive this change before the merge
