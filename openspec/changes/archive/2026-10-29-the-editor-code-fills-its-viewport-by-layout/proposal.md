## Why

Closes #1143, a bug under #419 (the code editor: an IDE-grade, write-once editor). A bounded `CodeEditor`
(`Height = Fill` or a fixed height) makes its code at least as tall as its viewport, so a press under the
last line lands in the code (#599). It did that with a number: the block's `MinHeight` was the height the
vertical scroll view reported after the frame.

On the web that number is not the room the code has. The sideways scroll view sits inside the vertical one,
and wherever a scrollbar takes space (Windows' classic scrollbars, which the runtime does not style) its
scrollbar stands under the code inside the vertical scroll view's content. Measured in Chromium with 17px
scrollbars: a two-line file with a long line in a pane 300 tall is 317 tall, and the pane scrolls by 17. The
number also lags a frame: the first frame draws the code as tall as the file, and a pane that shrinks runs
the code past it until the next report. And an editor with no line numbers put the sideways scroll view
straight inside the vertical one, where its `height: 100%` was exactly the viewport: a file longer than the
pane was cut at its bottom and did not scroll at all (measured: 900 of code in a pane 300 tall, nothing to
scroll).

## What Changes

For a developer using the SDK, nothing to write for the editor: a bounded editor's code fills its pane on
every target, from its first frame, and a short file never scrolls.

- **`ScrollView.FillViewport`.** A scroll view can say its content is never smaller than its viewport: on the
  scroll axis the content is at least as long as the viewport and scrolls only when it is longer; across, it
  takes the viewport's breadth unless it states its own. Decided by layout on both targets, with no number
  reported: on the web the scroller lays its child out as a growing flex item, over its content box, which
  leaves a scrollbar that takes space out; on Photon the scroll view measures its child again at the
  viewport's size where it came out smaller.
- **A scroll view stretched by its parent takes the stretched size on Photon**, as its `height: 100%` does on
  the web and as a box and a row already do there.
- **The editor fills by layout.** Both of a bounded editor's scroll views fill their viewports, and the row of
  gutter and code is always there, with or without the gutter, and stretches what it holds.
- **`CodeBlock.MinHeight` goes.** It existed for the number the editor handed it, and nothing else set it.
  Migration: a block that needs a least height sits in a box with that `MinHeight`.

The parts it reaches: the vocabulary (`ScrollView`), the web realizer and its TypeScript twin, Photon's layout
engine, the components (`CodeEditor`, `CodeBlock`) and their transpiled twins. The public surface moves:
`ScrollView.FillViewport` is added and `CodeBlock.MinHeight` removed. The developer surface does not move.
