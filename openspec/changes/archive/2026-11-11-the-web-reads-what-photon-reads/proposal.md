# Proposal

Closes #1329, #1331 and #1332, Bugs found by the semantics parity fixture of #339
(`SemanticsParityTests`).

## Why

The parity fixture reads every component with a semantic role on the web and on Photon and names each
difference. Three of them were defects of the web, or of both targets:

- A closed `Tooltip` was hidden with opacity alone, which leaves it in the accessibility tree, so a
  reader met it as a line of its own on top of hearing it as the trigger's description (#1329).
- A `TableView` read as a group gave its cells in the order its bands are written on the web, the
  pinned row, then the pinned column, then the body, where Photon reads them row by row (#1331).
- Every text field on the web carried a polite live region beside its input, so a message was written
  twice there, while Photon read it once and never announced it when it appeared (#1332).

## What Changes

- A hover panel at rest carries `visibility:hidden` on both realizers, and the theme's reveal rules
  show it on hover and on keyboard focus. It stays the trigger's description.
- A table view read as a group gives its cells an id and owns them, by row and then by column, from one
  element of no role and no size, on both realizers.
- `TextInput` draws its helper or error line in a `LiveRegion`, present from the first paint, so the
  message is announced when it appears and read once on both targets. The entry's description twin on
  the web is hidden instead of live, and stays the field's description.
- The parity baseline loses the entries these close. The opened `DatePicker` reads the field's message
  line on either side of its calendar, the position difference #1330 already names for its dismiss
  button, and gets an entry citing #1330. The spreadsheet's status region is #1333's, and its entry
  says so.

## Capabilities

### New Capabilities

- `screen-reader-parity`: what a reader hears of a tooltip and of a field's message, the same on every target

### Modified Capabilities

- `table-viewport`: a viewport read as a group reads its cells row by row on every target

## Impact

The web realizer and its TypeScript twin, the theme stylesheet, `TextInput`. No API, no setting.
