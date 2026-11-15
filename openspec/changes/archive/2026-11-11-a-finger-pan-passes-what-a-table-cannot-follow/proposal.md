## Why

Closes #1007, a Bug under #981 (the spreadsheet: a grid that stands beside Excel and Google Sheets). The
wheel passed what a table could not take at its end to the scroller around it (#982), and a finger did not:
on Photon a finger on the cells was a candidate pan of the table, and while it was the host armed neither the
scroller around the table nor a drag surface inside a cell; on the web the viewport declares `touch-action:
none` and took every pointer itself. On a phone, a table that fills the width of a vertical page and stands at
its last row left the page impossible to scroll with a finger over it, and a drag surface in a cell never got
its drag.

## What Changes

- A finger's pan the table can no longer follow on an axis moves the scroller around it on that axis: the
  finger's travel along it, less what the grid took since the table took the pan, as a nested scroll does on
  iOS and Android and Flutter's two-dimensional scrollables do. A flick of what passed on carries on, as a
  flick of the page's own pan does, and reduced motion lands it at once.
- The table reads the finger against where the pan began on screen, mapped as the frame it began in mapped
  it, so the table moving under a still finger, because the page took part of the pan, is no travel of the
  finger.
- A drag surface inside a cell keeps its gesture: the table's candidate pan lets go of a press that begins on
  it, on Photon as it already did for a sheet's resize grip, and on the web for any `[data-eq-drag]` in the
  viewport.
- On both targets the pan passes, on each axis, to the nearest scroller around the table on that axis: a table
  in a strip that scrolls sideways inside a page that scrolls down hands what goes across to the strip and what
  goes down to the page. On the web, when no element around scrolls an axis, the page itself takes it, since
  the document scrolls there; Photon's window never scrolls, so a page there is always a `ScrollView`.
- Nothing a developer writes changes, and neither the public surface nor the developer surface moves. Parts
  reached: Photon's host and the runtime's table half.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `table-viewport`: what the viewport cannot take passes to the scroller around it from a finger as from a
  wheel, and a drag surface in a cell keeps its gesture.
