# Tasks

## 1. The vocabulary

- [x] 1.1 `Pressable.CanRequestFocus`, true by default, and a row of the list sets it false; the
      declarative `Pressable(…)` takes it as `canRequestFocus:`
- [x] 1.2 `CodeSurface.Options`, `OptionsOrigin` and `HighlightedOption`
- [x] 1.3 Check: the vocabulary's contract tests (the runtime's twins of the nodes, the factories'
      conformance, the public API) take the new members

## 2. Photon

- [x] 2.1 The surface measures its options with loose constraints and lays them at their origin
- [x] 2.2 They paint over the code and the caret, and their rows take the press over the code's; a
      press on the list that no row takes is the list's
- [x] 2.3 A press on a pressable that cannot request focus ends no editing, focuses nothing, and
      makes no Tab stop
- [x] 2.4 The options announce as options after the code field, the highlighted one selected
- [x] 2.5 Check: `PhotonHost` drives an editor through typing, the arrows and a press
      (`CodeEditorCompletionTests`), a golden of the list at the caret in both modes, and each
      piece proved failing without its fix

## 3. The web

- [x] 3.1 The C# realizer and the runtime's lowering draw the options at their origin inside the
      surface, over the caret
- [x] 3.2 The input says it completes from a list while one shows, names it and points at the
      highlighted option; the rows are numbered as an `Anchored` listbox numbers its rows
- [x] 3.3 A pressable that cannot request focus cancels the press's focus move and leaves the Tab order
- [x] 3.4 Check: the DOM lowering in vitest through the transpiled editor, the server's HTML, and the
      dashboard sample in a browser (typing opens the list, the arrows move the active descendant, a
      press accepts while the textarea keeps the focus, a list on the last visible line stands above
      it, Escape closes it)

## 4. The component

- [x] 4.1 `CodeEditor.Completions`, the built-ins by default, handed to the controller when it holds
      other providers
- [x] 4.2 The list's view: rows of the code's lines, the matched characters, the kind, the detail, the
      selected entry washed, its documentation, a page of rows following the selection
- [x] 4.3 The placement: under the word, above when below does not fit, kept inside the viewport
- [x] 4.4 A press on a row selects and accepts it
- [x] 4.5 A bounded editor's code fills its viewport, and a press under it is the end of the document
      (#599)
- [x] 4.6 Check: the twins regenerated and executed (vitest, the conformance suite on both sides), the
      editor's tests on Photon, and the sample in a browser

## 5. On the way

- [x] 5.1 eqc: an enum member named `Value` or `HasValue` is its name (#631)
- [x] 5.2 eqc and the runtime: a helper class that takes the build context compiles in the runtime's
      build, `typeScale` is required, and `TypeStyle` scales as .NET does (#632)
- [x] 5.3 Reported, with what the tests fence: a bordered box on Photon (#629), a dense row's touch
      margin under Comfortable density (#630), a null-conditional read that is `undefined` (#633)

## 6. The author's review

- [x] 6.1 A press outside an editor is not its code's, and the list's area is clipped with the code
- [x] 6.2 No `aria-expanded` on the code input
- [x] 6.3 A provider the app added to the controller stays beside the editor's
- [x] 6.4 A row longer than the list fits its columns; the documentation is laid out only as far as it shows
- [x] 6.5 Check: a test for each, proved failing without its fix, including an answer that arrives after
      the key and the list's placement at the right edge and when the code scrolls sideways

## 7. Documentation

- [x] 7.1 The wiki's code editor and declarative surface pages, in English and Portuguese
- [x] 7.2 `docs/FLUTTER-PARITY.md` (`canRequestFocus`, `RawAutocomplete`), the plan's slice 3 row and
      the `docs/LEDGER.md` line citing #297
- [ ] 7.3 Archive this change before the merge
