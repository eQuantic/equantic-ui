## Why

Closes #1006, a Bug under #981 (the spreadsheet: a grid that stands beside Excel and Google Sheets). A
`TableView` or a bare `TableViewport` asked to fill an axis with no bound, which is what a page that
scrolls on that axis hands its content, took the whole visible extent of the nearest scroller around it.
When the table does not start at the top of that scroller, its own end, and its horizontal scrollbar with
it, landed below the fold by however far down it starts: about 36 px on the dashboard's `/table` screen,
whose page is 844 px tall and starts the table below its padding.

## What Changes

- The viewport takes what the scroller shows from where it starts in the scroller's content, so its end
  stands where the scroller's visible part ends, when that is at least half of what the scroller shows.
  Further down a long page, what is left is a sliver, and it takes the whole extent instead, as before.
- On the web, `table-viewports.ts` reads where the viewport starts in the scroller's content (or the
  page's, when the window scrolls it), the same whether the scroller is scrolled or not.
- On Photon, a `ScrollView` measuring its content collects the viewports that take its extent, reads
  where each one starts in that content after the measure, and measures the content again, once, when
  one started elsewhere than the measure assumed. Where it started is kept by the viewport's scroll model
  for the next frame, so a steady page measures once.
- The development log says which of the two the viewport took, on both targets.
- Nothing a developer writes changes: the same `SizeValue.Fill` lays out where it should. Neither the
  public surface nor the developer surface moves. Parts reached: the runtime (web realizer's table half)
  and Photon's layout.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `table-viewport`: the unbounded-axis requirement takes what is left from where the viewport starts.
