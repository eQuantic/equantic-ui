- **2026-11-09 · A table below the top of its page ends where the page's visible part ends**: a table
  viewport asked to fill an axis with no bound took the whole visible extent of the scroller around it,
  so one that starts below the top of that scroller ended, its horizontal scrollbar with it, below the
  fold by however far down it starts, about 36 px on the dashboard's `/table` screen. It now takes what
  the scroller shows from where it starts in the scroller's content, and the whole extent when what is
  left is under half of it, on the web from the box the browser laid out and on Photon from where the
  scroller placed it, measured again once when that moved and kept for the next frame. The development
  log says which of the two it took ([#1006](https://github.com/eQuantic/equantic-ui/issues/1006)).
  Proposed and archived through OpenSpec (`openspec/specs/table-viewport`).
