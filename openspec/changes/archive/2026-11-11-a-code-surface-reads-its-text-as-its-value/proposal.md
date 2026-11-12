# Proposal

Closes #1333, a Bug.

## Why

A code surface draws its text itself and takes the keyboard through a door the platform reads as a
field. On the web that door was a `textarea` holding nothing, labelled by the surface (`Name box`,
`Formula bar`, `Code editor`): a reader heard an empty field and then the drawn text (`A1`) as a line of
its own, and the spreadsheet's field the same way. On Photon the code field consumed its subtree and
carried no value, so the text was not read at all. A reader should hear the field and what it holds as one
thing, on both targets.

## What Changes

- The vocabulary says what a reader hears a code surface hold: `ICodeSurfaceModel.Reading`, a
  `TextReading` of the text and the selection inside it, in UTF-16 code units of that text.
- The code engine reads the lines around the primary caret, ten on each side, with the primary
  selection counted in them, so a keystroke in a long file never rewrites all of it. The formula bar
  reads the entry while a cell is edited and the active cell's value at rest, and the name box what is
  typed into it or the active cell's address.
- The web writes the reading as the door's content and keeps the door's value and selection in step
  with the model after each render, never while an input method composes; what the surface draws is
  `aria-hidden`. Photon gives the code field the reading's text as its value.
- The spreadsheet's door holds what the grid says now (`SheetSurface.Announcement`), the value Photon
  already gives that field.

The spreadsheet's polite status region, which announces the grid's moves on the web while Photon makes
the field itself live, stays: whether a live field is announced as reliably as a status region needs a
measurement with screen readers, and is its own issue, #1444.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `screen-reader-parity`: a code surface is read as a field holding its text

## Impact

`eQuantic.UI.Primitives` gains `TextReading` and `ICodeSurfaceModel.Reading`: an implementation of the
interface outside the SDK has one member to add. The code and sheet engines, both realizers' code and
sheet surfaces, Photon's semantics.
