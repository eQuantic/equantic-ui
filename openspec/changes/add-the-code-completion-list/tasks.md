# Tasks

## 1. The vocabulary

- [ ] 1.1 `Pressable.CanRequestFocus`, true by default, and a row of the list sets it false
- [ ] 1.2 `CodeSurface.Options`, `OptionsOrigin` and `HighlightedOption`
- [ ] 1.3 Check: the vocabulary's contract tests (the runtime's twins of the nodes, the enum and
      member baselines) take the new members

## 2. Photon

- [ ] 2.1 The surface measures its options with loose constraints and lays them at their origin
- [ ] 2.2 They paint over the code and the caret, and their rows take the press over the code's
- [ ] 2.3 A press on a pressable that cannot request focus ends no editing and focuses nothing
- [ ] 2.4 The options announce as options, the highlighted one selected
- [ ] 2.5 Check: `PhotonHost` drives an editor through typing, the arrows and a press, and a golden of
      the list at the caret

## 3. The web

- [ ] 3.1 The C# realizer and the runtime's lowering draw the options at their origin inside the
      surface, over the caret
- [ ] 3.2 The input says it completes from a list, and while one shows names it and points at the
      highlighted option; the rows are numbered as an `Anchored` listbox numbers its rows
- [ ] 3.3 A pressable that cannot request focus cancels the press's focus move
- [ ] 3.4 Check: the DOM lowering in vitest, and the sample in a browser

## 4. The component

- [ ] 4.1 `CodeEditor.Completions`, the built-ins by default, handed to the controller when it changes
- [ ] 4.2 The list's view: rows of the code's lines, the matched characters, the kind, the detail, the
      selected entry washed, its documentation, a page of rows following the selection
- [ ] 4.3 The placement: under the word, above when below does not fit, kept inside the viewport
- [ ] 4.4 A press on a row selects and accepts it
- [ ] 4.5 Check: the twins regenerated and executed, the editor's tests on Photon, and the sample in a
      browser and in a window

## 5. Documentation

- [ ] 5.1 The wiki's code editor page, in English and Portuguese
- [ ] 5.2 `docs/FLUTTER-PARITY.md` (`canRequestFocus`), the plan's slice 3 row and the
      `docs/LEDGER.md` line citing #297
- [ ] 5.3 Archive this change before the merge
