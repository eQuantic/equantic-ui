# Photon names a combobox by its label

Closes #1328, a bug found by the semantics parity fixture of #339 (`SemanticsParityTests`).

## Why

A `Select(["Lisbon", "Porto"], 0, ...)` was announced on the web as a combobox with no name, since it has no label,
whose value is `Lisbon`. On Photon it was a combobox NAMED `Lisbon` with no value, and named by its placeholder with
nothing chosen. The vocabulary's contract is the web's: a combobox's name is its label, and what its field shows is
its value. A reader on Photon heard the option where the field's name belongs, and a labelled one heard it twice.

## What changes

- Photon's semantics name a combobox by the pressable's label, and by nothing without one. What its field shows, the
  choice or the placeholder, is always its value, as the web reads it. The macOS bridge hands it over as the
  `AXPopUpButton`'s value, and the iOS and Android bridges as theirs, as they already did for a labelled one.
- The eight `Select` entries of the semantics parity baseline that waited on #1328 are removed.

For a developer nothing is written differently. A `Select` has no label of its own, so it has no name on either
target, as before on the web; giving it one is a feature of its own. No public surface and no developer surface moves.

## Principles

It follows 4 (one reading on every target, the web's, which is the vocabulary's contract) and 5 (the name is the
author's word, never a target's guess from the content).
