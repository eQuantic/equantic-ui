# Tasks

## 1. The models

- [x] 1.1 `ClaimsEscape(KeyboardConvention)` on `ICodeSurfaceModel` and `ISheetSurfaceModel`, implemented by the
      code editor's controller (through its keymap's bindings), the sheet, its formula bar and its name box, with
      the public API declared and the transpiled twins regenerated
- [x] 1.2 A test that each model's answer is what its Escape then does, in each state that claims it and at rest,
      on both keyboard conventions

## 2. The realizers

- [x] 2.1 The web: the shortcut controller asks the focus with `eq:takes-key-first` before a binding fires, and the
      text door keeps Escape while its model claims it
- [x] 2.2 Photon: `KeyDown` leaves a binding of Escape to the code or sheet surface that claims it, and `Back`
      fires the binding itself
- [x] 2.3 Each test fails first: an editor with its list open and a sheet with a cell being edited, each in a
      dialog, on Photon and in the runtime's specs, and the controller's question in the shortcut spec
- [ ] 2.4 Check in Chromium on the dashboard sample's `/code` page, with a `CodeEditor` in a `BottomSheet`: Escape
      with the completion list open closes the list and keeps the sheet and the keyboard in the editor, and the
      next Escape closes the sheet

## 3. Documentation

- [x] 3.1 The wiki pages that say what Escape does in a code editor, a spreadsheet and a dialog, in every language
      they are written in
- [x] 3.2 One ledger entry citing #1181, as a file of its own under `docs/ledger/`
