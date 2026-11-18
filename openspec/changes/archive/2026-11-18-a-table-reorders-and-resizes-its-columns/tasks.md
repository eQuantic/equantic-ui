## 1. The vocabulary

- [x] 1.1 `AdjustableRole.Separator` and `SemanticRole.Splitter`, appended; `Adjustable.Axis`, horizontal by default;
  the enum baseline and the public API.
- [x] 1.2 The C# and TS lowering announce a separator with a value as `separator` and one without as `group`, and
  write the orientation across the axis for a separator and a vertical slider; the Photon semantics walk gives a
  separator the `Splitter` role with its range, and `NativeRole` maps it for AppKit, UIKit and Android.
- [x] 1.3 `SortableItem.Label`, written as `data-eq-sort-name` and read by the web's controller and the Photon host
  before what the item's subtree says; `UI.SortableItem`'s `label`.
- [x] 1.4 Check: realizer tests and vitest read the attributes on both sides, and the parity fixture lowers a
  separator identically; a Photon test reads the `Splitter` node and its range; a named item writes its name and is
  said by it on both targets. Each fails without its change.

## 2. The table

- [x] 2.1 `DataColumn` gains `MinWidth` (64) and `MaxWidth` (1024); `TableTheme` gains `ColumnDropColor` and
  `ResizeHandleColor`, merged and carried by the theme's twin; `SdkResources` gains `ResizeColumn`, `TableColumns`
  and `SortByColumn` in every culture, read through `SdkStrings`.
- [x] 2.2 `DataTable` becomes a `StatefulComponent` with `ColumnOrder`, `OnColumnsReordered` and `OnColumnResized`:
  the columns drawn in order, the cells read through it, the header a horizontal sortable while the app keeps the
  order, the drop line while a column is carried, and a separator handle at each fixed column's edge while the app
  keeps widths.
- [x] 2.3 Check: realizer tests read the header's sortable and items, the handles only on fixed columns and the
  cells in order; vitest drags a heading and a handle in the DOM and moves both by the keyboard, reading each report;
  a Photon host test moves a column and resizes one by the keyboard; each fails without its change.

## 3. The sample

- [x] 3.1 The dashboard's payments table keeps an order and widths, its fixed columns resizable.
- [x] 3.2 Check: in a browser, the table reorders its columns and resizes one by pointer and by keyboard; the
  `DefaultUIDashboard` and `PhotonDesktop` samples build.

## 4. Records

- [x] 4.1 Public API in the branch's files; `docs/FLUTTER-PARITY.md` gains the table's row; the ledger entry.
- [x] 4.2 The wiki's `DataTable` section says how an app keeps the order and the widths, in five languages, on the
  branch's wiki branch.
- [x] 4.3 Check: the affected suites, the runtime's tests, `check-openspec.sh`.
