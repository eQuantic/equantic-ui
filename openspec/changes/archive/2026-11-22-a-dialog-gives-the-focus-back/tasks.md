# Tasks

## 1. The focus given back

- [x] 1.1 Web: `focus-trap.ts` records the opener's place among the controls Tab reaches beneath the layer. When the opener is gone, it focuses the control now at that place.
- [x] 1.2 Photon: `PhotonHost.ModalFocus` records each modal layer that opens, with the focus it had and that focus's place among the stops beneath it, and gives the focus back when the layer closes. A steady frame allocates nothing.
- [x] 1.3 Photon: a modal layer that opens takes the focus, on `InitialFocus` or on its first stop, and keeps the ring as it was.

## 2. Proof

- [x] 2.1 `ModalFocusReturnTests` (Photon) and `focus-trap.spec.ts` (web) run the same rows: back to the opener, the next row, the last row, and a dialog over a dialog. Every case fails without the change, and the two deleted-row cases fail with a give-back that has no fallback to the place.
- [x] 2.2 `ModalFocusTrapTests` begins its walks on the dialog's first control. The overlay frame cost stays under its ceiling.

## 3. Documentation

- [x] 3.1 The wiki's Components and Runtime pages, in all five languages.
- [x] 3.2 A ledger entry under `docs/ledger/`.
