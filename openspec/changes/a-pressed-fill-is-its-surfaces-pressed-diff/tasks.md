# Tasks

## 1. Measured on main

- [x] 1.1 Draw each component that sets `PressedBackground` pressed, on both targets, and record the fill. Verify: each draws the token it sets today, the web under the `!important` rule and Photon through `PendingFill`. Measured: the colours agree; the place does not for a tab (Photon 133x3, the strip under its label) and for the bar and the rail (the web fills the whole item)

## 2. One mechanism

- [x] 2.1 Move each component's pressed fill to its surface's `Pressed.Background`, on the box the handoff names. Verify: `PressedFillPlacementTests` (Photon, simulated and held) and `PressedFillPlacementRealizerTests` (web) pin the same eight components to the same places and colours
- [x] 2.2 Remove `Pressable.PressedBackground`, the factory's `pressedBackground` parameter, `PendingFill`, `--eq-pressed-bg`, `eq-press-fill`, `eq-pressed` and the two `!important` rules, on both web producers. Verify: no reference left in `src`, `samples` or `tests` but the guards that assert their absence, and the two `PublicAPI` files carry the `*REMOVED*` lines
- [x] 2.3 Regenerate the transpiled twins. Verify: the only change in each is the moved fill
- [x] 2.4 Guard the order the `!important` stood in for. Verify: `APressedFill_OutranksEveryOtherFillOfItsBox_BySpecificityAlone` reads the computed specificity of the pressed fill's selector against the box's own, its hover's and its focus's, and fails when the pressed family is weakened to `.eq-pressable:active` (measured: (0,3,0) against the focus's (0,3,0))
- [x] 2.5 Measure in Chromium the computed `background-color` of the SDK's own output (the theme stylesheet, the atomic sheet and the lowered HTML of a button, a selected filter chip, tabs and a navigation bar), each state's pseudo-class rewritten as a class of the same weight so a press can be held. Verify: pressed is the token the component set on main, hover, focus and press together are the press, and the fill lands on the surface, the cell or the pill. The hydrated classes are the served ones by the parity fixture, which compares the C# and TypeScript lowerings of the same trees
- [x] 2.6 Stop a control's press and focus at a control nested in it, pictured or real, on the web's two producers and on Photon. Verify: `ANestedControlKeepsItsOwnStateTests` (Photon, held, focused and pictured) fail at three of five with Photon's old scope and pass with the new; `ANestedControlKeepsItsOwnStateRealizerTests` pin both families' selectors and the picture; the parity fixture's `nested-control-simulated` case holds the two web producers together; and Chromium, measuring the SDK's own output, draws a pressed row with its button at rest, a pressed button with its row at rest, and the same for focus
- [x] 2.7 Run the Web, Native.Engine, Compiler and Conformance suites whole, the last two through the suite lock, and the runtime's. Verify: each one's total and exit code

## 3. Documentation

- [x] 3.1 The wiki's Styling and DeclarativeSurface pages in all five languages, on a wiki branch named like this one, the nested control's rule included
- [x] 3.2 The `WidgetStateProperty` row of `docs/FLUTTER-PARITY.md`
- [x] 3.3 One entry for the ledger citing #616, as a file of its own under `docs/ledger/`, carrying the migration line
