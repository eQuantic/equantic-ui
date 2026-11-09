# Tasks

## 1. Measured on main

- [ ] 1.1 Draw each component that sets `PressedBackground` pressed, on both targets, and record the fill. Verify: each draws the token it sets today, the web under the `!important` rule and Photon through `PendingFill`

## 2. One mechanism

- [ ] 2.1 Move each component's pressed fill to its surface's `Pressed.Background`. Verify: the same fills as 1.1, on both targets, simulated and held
- [ ] 2.2 Remove `Pressable.PressedBackground`, the factory's `pressedBackground` parameter, `PendingFill`, `--eq-pressed-bg`, `eq-press-fill`, `eq-pressed` and the two `!important` rules, on both web producers. Verify: no reference left in `src`, `samples` or `tests`, and the two `PublicAPI` files carry the `*REMOVED*` lines
- [ ] 2.3 Regenerate the transpiled twins. Verify: the only change in each is the moved fill
- [ ] 2.4 Press a button in a browser on main and on the branch. Verify: the computed `background-color` while held is the same, a hovered one too, and the hydrated classes are the served ones
- [ ] 2.5 Run the Web, Native.Engine, Compiler and Conformance suites whole, the last two through the suite lock. Verify: each one's total and exit code

## 3. Documentation

- [ ] 3.1 The wiki's Styling and DeclarativeSurface pages in all five languages, on a wiki branch named like this one
- [ ] 3.2 The `WidgetStateProperty` row of `docs/FLUTTER-PARITY.md`
- [ ] 3.3 One entry for the ledger citing #616, as a file of its own under `docs/ledger/`, carrying the migration line
