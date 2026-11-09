# Tasks

## 1. Photon

- [x] 1.1 `RealizeResult` answers the root of the topmost modal and open layer, gated as the semantics walk
      announces a dialog
- [x] 1.2 `PhotonHost`'s focus walk visits only the stops built under that layer and wraps among them, and holds
      the keyboard when the layer has none
- [x] 1.3 Each test fails first: Tab and Shift+Tab wrapping in a dialog over a page's button, a dialog over a
      dialog, a menu open in a dialog, a toast over a dialog, a toast alone, and a dialog with no control
- [x] 1.4 The `modal-focus` scenarios on Photon: a code editor last in a dialog indents on Tab, one first outdents
      on Shift+Tab, and after Escape the walk cycles inside the dialog

## 2. Documentation

- [x] 2.1 The wiki pages that say what a modal layer does with Tab, in every language they are written in
- [x] 2.2 One ledger entry citing #1066, as a file of its own under `docs/ledger/`
