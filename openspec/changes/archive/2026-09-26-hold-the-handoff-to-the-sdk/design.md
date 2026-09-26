# Design

## How Flutter answers it

Flutter keeps no contrast gate in the framework: Material 3's tooling solves a scheme's tones, and
the framework draws them. The part of this change that follows Material 3 is the dark scheme's shape,
lighter fills with dark text on them. The part that goes past it is the gate: here the palette is
measured by a test on every build, in both themes, so a value that drifts under a floor fails instead
of shipping.

## Decisions

- **Mark the figures, then count the marks.** A page repeats token values in prose, and a sentence is
  a second copy no pin reads. Marking each figure with its path lets the test compare the text with
  the token, and the ratchet on the number of marks stops a re-export from passing by leaving nothing
  to compare.
- **A status is a probe, not a badge.** Each request names the line of a `PublicAPI` file or the
  source reference that proves it shipped. A request whose probe appears fails until its entry and its
  pages move, and a shipped item whose probe vanished fails too, so a release moves the pages or
  breaks the build.
- **APCA 0.0.98G, with a 0.05 tolerance.** The palette was solved to land on some floors, and a
  last-bit difference in `Math.Pow` between runtimes must not turn a pass into a failure. The formula
  first reproduces the reference values, so a wrong constant fails before any pair is judged.
- **The floor of 24dp under a pointer.** The control ladder's smallest Compact target is 26dp, but the
  selection box of Checkbox and Radio is 20dp, below WCAG 2.2 SC 2.5.8. The decision is recorded
  here, and the change to `ExpandHitRect` and to the web's fine-pointer rule is #430, because it moves
  hit-testing on every target and deserves its own test.
- **Corners where the target can draw them.** The web takes the continuous profile through
  `corner-shape` as a progressive enhancement, so the difference between two readers is their
  browser, not the target. The curve is named for what it is, a superellipse, as Flutter's
  `RoundedSuperellipseBorder` does, rather than SwiftUI's `Continuous`.
