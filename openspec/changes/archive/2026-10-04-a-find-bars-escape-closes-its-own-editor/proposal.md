# Proposal

Fixes #457: a code editor's find bar closed on Escape through a page-wide chord, mounted with the bar.
Fixes #567 on the way: four controls mounted their chords with their panel, and moved their trigger.

## Why

- Two editors on one page with their bars open: Escape closed the bar of the editor mounted last,
  wherever the keyboard was, since a page-wide chord answers for the page and the last one declared
  wins.
- Made the editor's own, around the code, the chord would have bound Escape while the bar is closed
  too, and taken the key from a dialog around the editor. Mounted only while the bar is open, around
  the code, it would have moved the code in the tree each time the bar opened, a new surface whose
  scroll starts over (the reason ⌘F already wraps the layers whether the bar is open or not).
- What was missing is the state Flutter gives an `Action`: in the tree and not enabled, so the key
  goes on to whatever else would take it.
- Inside a dialog with the bar open, the web answered Escape with the dialog's, where Photon closed
  the bar: of two nested shortcuts of one chord, Photon's frame lists the outer one first and the web
  declared it last, each `Shortcut` declaring after its child lowered, and both walk the list from
  its end.

## What Changes

- `Shortcut.Enabled`, true by default: a shortcut that is not enabled stays in the tree and binds
  nothing, on the web (no marker, no declaration, in SSR and in the TypeScript twin) and on Photon (no
  binding in the frame).
- The code editor's Escape wraps the layers beside ⌘F, focus-scoped and enabled only while the bar is
  open: it closes the bar of the editor the keyboard is in, the bar or the code, and binds nothing
  while the bar is closed.
- A web `Shortcut` takes its place in the list before its child lowers, so a binding is listed before
  the ones inside it, as on Photon, and the inner of two nested chords answers first.
- `Select`, `Menu`, `TimePicker` and `DatePicker` keep their chords around their tree, enabled while
  their panel is open. Mounted with the panel, a `Shortcut` per chord moved the trigger down the tree
  when it opened, and on Photon the keyboard focus named a path the trigger had left.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `keyboard-shortcuts`: a shortcut that is not enabled binds nothing, the inner of two nested
  shortcuts of one chord answers, the find bar's Escape is the editor's own, and a control's chords
  stay in the tree while its panel is closed.

## Impact

- **Vocabulary**: `Shortcut.Enabled` (an addition to the public surface).
- **Realizers**: the web's SSR lowering, its TypeScript twin and the browser's shortcut list, and
  Photon's emit visitor.
- **Components**: `CodeEditor`, `Select`, `Menu`, `TimePicker` and `DatePicker`, and their transpiled
  pins.
