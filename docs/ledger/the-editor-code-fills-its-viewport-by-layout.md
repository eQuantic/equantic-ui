- **2026-10-29 · A bounded code editor's code fills its viewport by layout, and a short file never scrolls**: a
  bug under #419 ([#1143](https://github.com/eQuantic/equantic-ui/issues/1143)). The code was as tall as the height
  the vertical scroll view reported (#599), and on the web the sideways scroll view's scrollbar stood under it
  wherever scrollbars take space: measured in Chromium with 17px scrollbars, a two-line file with a long line in a
  pane 300 tall was 317 tall and scrolled by 17. The number also lagged a frame (the first frame drew the code as
  tall as the file, and a pane that shrank ran the code past it), and an editor with no line numbers cut a long
  file at its pane's bottom with nothing to scroll. `ScrollView.FillViewport` says the content is never smaller
  than the viewport, decided by layout on both targets: a flex line over the scroller's content box on the web,
  which leaves the scrollbar out, and a second measure at the viewport's size on Photon, where a stretched
  scroll view now takes the stretched size. Both of the editor's scroll views fill, its row of gutter and code is
  always there and stretches, and `CodeBlock.MinHeight` goes. Proved by Photon layout and component tests, the
  lowering specs and a parity case, each failing first, and in Chromium on the dashboard's `/code` page with
  scrollbars that take space. Proposed and archived through OpenSpec (`scrolling`, `code-editor`).
