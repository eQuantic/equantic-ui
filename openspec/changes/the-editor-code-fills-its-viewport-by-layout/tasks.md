# Tasks

## 1. The vocabulary and the realizers

- [ ] 1.1 `ScrollView.FillViewport` in the vocabulary and its TypeScript twin, with the public API declared
- [ ] 1.2 The web realizer and `lowerScrollView`: a scroller that fills is a flex line on its axis, its child
      grows and never shrinks, the child's `flex-shrink` replaced; a parity case nests two
- [ ] 1.3 Photon: a scroll view measures its child again at the viewport's size where it came out smaller, and
      a stretched hugging scroll view takes the stretched size

## 2. The editor

- [ ] 2.1 A bounded editor's scroll views fill their viewports, the row of gutter and code is always there and
      stretches, and `CodeBlock.MinHeight` goes; the transpiled twins regenerated
- [ ] 2.2 Each test fails first: the layout tests of a filled scroll view and the editor's first frame and
      shrinking pane on Photon, the lowering specs and the parity case on the web
- [ ] 2.3 Check in Chromium on the dashboard sample's `/code` page with the editor filling a tall box and
      17px scrollbars that take space: a short file with a long line does not scroll vertically, a press under
      its last line puts the caret on the last line and the textarea has the focus, and an editor with no line
      numbers scrolls a long file

## 3. Documentation

- [ ] 3.1 The wiki's CodeEditor page, in every language it is written in, and the scroll view's page where it
      lists the scroll view's members
- [ ] 3.2 One ledger entry citing #1143, as a file of its own under `docs/ledger/`
