# A modal layer in a hidden adaptive arm lets go of the keyboard

Closes #1185, a bug found on 2026-09-23 while fixing #360 (the console mounts its page once).

## Why

The web mounts every arm of an `AdaptiveNode`, and its CSS shows one. A drawer left open in the compact arm keeps
its trap marker when the window widens and the arm goes hidden, and the focus trap controller took every marked layer
as a trap: Tab was prevented and a click on the page was pulled back into a layer nobody could see. Since #1783 the
browser holds one arm once the page has hydrated, so a crossing renders the other arm. A served page still carries
every arm until it hydrates, though, and a render that rebuilt only what was marked draws the arms it holds behind
their gates until the page renders whole.

## What changes

- The browser's focus trap controller asks the gates around a layer when the Tab or the focus arrives, from the media
  conditions the gates' CSS applies, as the shortcut dispatcher asks them around a binding (#363). The topmost layer
  whose every arm is on screen owns the keyboard, and a layer in a hidden arm is not where the focus is given back.

For a developer nothing is written differently: a drawer open in an arm the window hides lets the page have the
keyboard, and takes it back when its arm shows again. No public surface and no developer surface moves; Photon lays
out one arm and never had the defect.

## Principles

It follows 9 (the structural fix, the same question the shortcut dispatcher already asks) and 4 (the web realizes
what Photon does: a hidden arm holds nothing).
