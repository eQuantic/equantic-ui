## Context

The controller held one selection, `_selection`, and every command read and wrote it: a motion moved its
focus, an edit replaced it, the history recorded the one range before and after. The surface was shaped
for more from the start: `ICodeSurfaceModel.Carets` is "every caret, the primary first", and both
realizers paint the list. Since #1031 an undo step holds several edits, made one after another, and the
editor raises one `CodeChange` for a step once it is made, with its problems carried as the document takes
each replacement.

## Goals / Non-Goals

**Goals:** VS Code's and Rider's multiple carets on both targets from the same C#: adding them (keys,
pointer, occurrences, column selection), every command at every caret as one undo step, the clipboard and
the completion at every caret, the carets drawn.

**Non-Goals:** column selection by keyboard (⇧⌥⌘ and an arrow), undo of a caret's placement (⌘U), the
find bar's "select every match", an input method's composition shown at every caret (it composes at the
primary, and what it commits is typed at every caret); each can follow on the same model.

## Decisions

- **The primary is `_selection`, the others a list beside it.** `_others` holds the carets besides the
  primary in document order, each a selection and the cell a run of ↑ and ↓ aims at (`CodeCaret`). With
  one caret the list is empty and every command runs exactly as it did, which the engine's whole suite and
  its seeded property test prove. `Selections` answers the primary first and the others in document
  order; setting it makes the first the primary. `Selection`'s setter leaves that one selection.
- **The carets that meet are made one, as VS Code merges its cursors**: two selections that overlap, or a
  caret where another selection begins, ends or is; two selections that only touch stay two. The primary
  stays the primary, grown by what it took in.
- **The primary is the caret placed last**, as Rider's `CaretModel.getPrimaryCaret` is: the caret ⌥⌘↓
  added furthest down, ⌘D's new occurrence, the one ⌥ and a press placed. It is the one brought into
  view, the one an input method composes at and the completion list, the signature panel and the card
  follow, and the one Escape keeps. VS Code keeps its first cursor as the primary and reveals the newest
  one apart from it; one caret that is both the newest and the revealed one is one rule where VS Code has
  two.
- **A command written for one caret runs at every caret** (`AtEveryCaret`): from the last caret to the
  first, each run on text no caret before it in the document has moved, all as ONE undo step and one
  change heard. Each caret stands where its own run left it, then carried through the edits the carets
  before it made afterwards. Those edits all lie before it, so they move what follows by a number of lines
  and the rest of their last line by a number of columns: composed from the first caret on, that is one
  line, one line count and one column count (`Place`), so ⇧⌘L over thousands of words places them in time
  linear in their number. A caret whose text a later caret's edit already took is not run: it goes where
  that edit left its text and is made one with the caret that took it. A word deleted to the right stops
  where the next caret's text begins, as VS Code keeps the first of two deletions that would overlap.
- **A line command runs once for each block of carets** (`AtEveryBlock`): the carets whose lines share a
  line make one block, and so do the ones whose lines follow each other for a move, which would otherwise
  pass the other block's line. The block's command answers where each of its carets stands. Tab, ⇧Tab and
  ⌘/ are an edit a line now, at column 0 or at the marker, so a problem on an indented or commented line
  travels with its text, and two carets on one line indent it once.
- **A step restores every selection.** The history keeps a `CodeStep`: its edits, and every selection
  before and after them. Typing at several carets is still one run: a typed step joins the step before
  when it is one typed character at each caret, each where the run's text ends at its caret. The run
  joins its steps edit by edit, so it keeps where each edit's text ends, read from the edits as the
  single-caret run kept its one end, and only a step made from the last caret to the first begins one.
- **Adding carets.** ⌥⌘↑ ⌥⌘↓ (Ctrl+Alt with the arrows on Windows) add a caret on the line above or below
  each caret at the cell it aims at, as VS Code's `addCursorUp` does for every cursor. ⌥ and a press adds a
  caret, or takes away the caret pressed on while there are others. ⇧⌥ and a press selects the column from
  the primary's anchor to the cell pressed, and a drag grows it: one selection per line, from the anchor's
  cell to the cell pressed; a line that ends before the box's left edge has none unless every line does.
- **Occurrences.** ⌘D with the primary empty selects the word at every empty caret, and its later presses
  search by whole word and by case; with a selection made otherwise it searches for the selected text,
  case kept. Each press adds the next match after the primary, wrapping at the end, as the new primary,
  and is claimed even when every match is taken, so the key never reaches a browser's bookmarks. ⇧⌘L
  selects every match; the one the caret stood in is the primary.
- **Escape's order** is the list, the card, the panel, then the other carets, then the trap on Tab.
- **The clipboard.** A copy with several carets takes each selection's text, a line each, or each caret's
  line when none selects anything, and remembers the pieces; a paste of that text at as many carets gives
  each its own piece, a text with one line for each caret gives each its line, and any other goes whole to
  every caret, as VS Code spreads a paste.
- **The completion** asks at the primary, and an entry accepted replaces, around every caret, what it
  replaces around the primary, as VS Code applies one suggestion at every cursor. The word's start and a
  provider's range are carried through every edit made since they were read, at whichever caret: a caret
  before the primary on its line moves the word, and one after it moves the line's end, which the
  provider's range used to be measured from. What is typed at the range's start or end is in it, as it
  was measured from the line's end, so an arrow after typing cannot leave a character out of it.
- **Drawing.** `Carets` lists every caret, the primary first. `SelectionsIn` and `CaretLinesIn` answer the
  selections and the carets' lines a view's window reaches, and `SelectionBandsIn` every one's bands; the
  code block takes `ActiveLines` and `Selections`, so every caret's line is washed and every selection's
  glyphs take the theme's selected ink. The realizers paint every caret they are given already; the web
  surface keys its input, so the textarea is the same element whatever stands around it: matched by place,
  a press that added carets while a note appeared matched it with a new caret's mark, made it anew, and
  left the keyboard on the page, which the browser in the dashboard showed and a test now pins.

How Flutter solves it: it does not. `TextEditingValue` holds one selection and `EditableText` one caret,
so a multi-caret editor in Flutter is built outside it. The model here follows VS Code's cursors (a
primary and others, merged after each command, one edit operation per cursor applied as one undo element)
and Rider's primary caret.

## Risks / Trade-offs

- Every command runs at every caret through one driver. → With one caret the driver is the command, and
  the engine's suite, the keymap's behaviour baseline and the seeded property test of #1033 hold unchanged;
  a seeded test proves a command at carets on lines of their own does at each what it does at one.
- Two carets whose commands reach each other's text. → A later caret's edit that takes an earlier caret's
  text makes the earlier one go with it, and a deletion to the right stops at the next caret's text; the
  cases are named in the spec and pinned.
- ⇧⌘L over a huge file. → The placement is linear, and the view asks for the selections, bands and lines
  of its window only.
- Escape keeps a different caret than VS Code's after ⌘D. → Stated in the spec and the wiki, with the
  reason.
