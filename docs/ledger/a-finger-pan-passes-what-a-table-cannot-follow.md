- **2026-11-09 · A finger's pan passes what a table cannot follow to the page around it**: the wheel
  passed what a table could not take at its end to the scroller around it, and a finger did not. On
  Photon the host armed nothing around a finger's candidate pan of a table, and on the web the viewport
  took every pointer (`touch-action: none`), so a table filling the width of a page at its last row left
  the page impossible to scroll with a finger, and a drag surface in a cell never got its drag. Both
  targets now move the nearest scroller around the table on each axis by the finger's travel along it
  less what the grid took, as a
  nested scroll does on iOS and Android, carry a flick of it on, read the finger against where the pan
  began so the page moving the table is no travel of the finger, and leave a press on a drag surface in a
  cell to that surface ([#1007](https://github.com/eQuantic/equantic-ui/issues/1007)). Proposed and
  archived through OpenSpec (`openspec/specs/table-viewport`).
