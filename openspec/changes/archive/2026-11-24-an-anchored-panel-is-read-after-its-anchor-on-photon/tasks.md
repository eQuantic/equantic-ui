# Tasks

## 1. The walk

- [x] 1.1 Find each anchored node's layer by the layer root's parent position
- [x] 1.2 Read the anchor, then its layer, in place, and skip that layer after the page
- [x] 1.3 Put a dialog panel's stop on the panel, after the scrim's dismiss button

## 2. Proof

- [x] 2.1 A menu in the middle of a page is read right after its trigger, and a dialog panel's stop follows the dismiss button and precedes its content; both fail with main's walk, and the dialog case fails with the stop left on the layer
- [x] 2.2 The semantics parity fixture passes without the four entries, and fails with main's walk and with the stop left on the layer
- [x] 2.3 The semantics, combobox, dialog, menu, select, focus and AppKit families pass with the new order

## 3. Documentation

- [x] 3.1 One entry under `docs/ledger/`
