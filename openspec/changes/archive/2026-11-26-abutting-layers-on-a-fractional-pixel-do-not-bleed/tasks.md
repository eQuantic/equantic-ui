# Tasks

## 1. The snap

- [x] 1.1 `FillRRect` snaps an axis-aligned fill's edges to whole device pixels, half up; turned or skewed fills and discs are left where they are; a sub-pixel fill keeps a pixel
- [x] 1.2 `PushClip` snaps an axis-aligned clip the same way

## 2. Proof

- [x] 2.1 `LayerSeamTests`: at 2.625 device pixels a dp, the row both edges cut holds none of the red behind; fails on main's display list
- [x] 2.1b The same for a closed `SwipeableRow`, whose box clips both layers; fails with the fills snapped and the clip not
- [x] 2.1c On the Android emulator (eq_a11y, 2.625), the wallet's closed row: a red-tinted pixel row under it on main and with the fill snap alone, none with the clip snapped too
- [x] 2.2 Each of the 76 diverging goldens audited before regenerating: no pixel away from a colour boundary changed
- [x] 2.3 The six tests that read a fractional fill say why each value moved
- [x] 2.4 The engine and testing suites pass

## 3. Documentation

- [x] 3.1 One entry under `docs/ledger/`
