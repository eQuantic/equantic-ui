## Context

The engine (`eQuantic.UI.Code`) holds the state of every interaction as a session on the controller, so a
test drives it with keystrokes and no screen (docs/CODE-EDITOR-PLAN.md, §5): the completion, the
problems, the hover, the signature panel, the snippet. Going to a line and to a symbol is one more
interaction of that kind, and the jump list is state the controller already has every input for: it
hears every move of the caret (`SelectionChanged`) and every edit (`Changed`).

What the engine cannot do today is move the VIEW without moving the caret. A host reveals the primary
caret, by the least movement, whenever `RevealVersion` changes; nothing else scrolls the code. VS Code's
"Go to Line" centres the line it previews while the caret stays, and gives back the exact view on
Escape; "Go Back" centres a place that is out of view. And under that, a `ScrollView` reads its `Offset`
on its first frame alone (the web applies it once on adoption, Photon's scroll clock owns the position
after the first frame), so no component can scroll its own view again.

## Goals / Non-Goals

**Goals:**

- ⌃G and ⇧⌘O as VS Code's quick access reads them, previewing in the code and restoring on Escape.
- A document-symbol contract an LSP adapter maps one to one, and that slice 6 (sticky scroll, a
  breadcrumb) and slice 7 (Roslyn) reuse.
- The jump list of VS Code's plain "Go Back", walked with its keys.
- Programmatic scrolling for any `ScrollView`.

**Non-Goals:**

- Definitions, references and ⌘-click: #1084, which records its jumps in this list.
- `::` offsets in the line field, the zero-based toggle beside them, and `@:` grouping by kind: VS Code's
  later additions, each a follow-up of its own if an app asks.
- A navigation history across documents: the editor holds one document, and an IDE that holds several
  owns its own history, as VS Code's lives in its workbench and not in Monaco.

## Decisions

- **The scroll request is Flutter's `ScrollController.jumpTo`, as data.** A `ScrollView` takes a
  `Request`, a `ScrollRequest(Version)` with an `Offset`, and each realizer honours a version once: the
  web sets the element's scroll position after the render that carries it, Photon hands the offset to the
  scroll clock for that view. A request still in the tree after the reader scrolled moves nothing,
  exactly as `TableScrollRequest` behaves for the table, which is the precedent. VS Code's preview
  scrolls smoothly, and an animated request (`animateTo`) belongs to the scroll view as a whole, so it
  waits for a change of its own: here the view jumps.
- **The controller knows the view it is drawn in.** `CodeEditorController.View` is the rectangle of the
  code on screen, in the surface's coordinates, reported by whoever draws it (the `CodeEditor`, from the
  two scroll views it already listens to). With it the engine answers what the reveal methods need,
  whether a position is out of view and what offset centres it, and remembers the exact view to give back
  on Escape. A scroll the engine asks for is `CodeEditorController.Scroll`, a request the component hands
  to its scroll views. The geometry stays in the engine, where every other position becomes a rectangle;
  the component only carries numbers across.
- **One session for both fields, keyed by the first character.** VS Code's quick access opens "Go to
  Line" as `:` in the quick open, and "Go to Symbol" as `@`. The session (`CodeGoTo`) does the same: its
  `Query` holds the prefix, ⌃G opens it with `:`, ⇧⌘O with `@`, and deleting the prefix and typing the
  other switches mode. Text with neither prefix lists nothing, and the field says how to start.
- **The line is read as `gotoLineQuickAccess` reads it.** The text after `:`, trimmed, splits on `,`, `:`
  or `#`; each part is read as JavaScript's `parseInt` reads it (leading digits, a sign, anything after
  ignored), a negative line counts from the end, a line past either end is clamped, and the column is a
  VISIBLE column, a tab counting the cells to its stop (the grid's `CodeLineCells` already maps both ways).
  With no column the target is the line's start.
- **Symbols are asked once per opening and flattened as VS Code's outline flattens them.** `CodeSymbols`
  asks every provider in `Providers` when the field opens on `@`, joins their answers in that order, and
  flattens each tree depth-first, each symbol carrying its parent's name as its container
  (`OutlineModel.asListOfDocumentSymbols`). Typing filters the same answer; nothing is asked again until
  the field opens again. A late answer is dropped by its generation, as every session drops one.
- **The filter is the completion's matcher.** The text after `@`, trimmed: its first word is matched
  against the symbol's name and the others against its container, by `CodeFuzzyMatch`; entries rank by
  score, then by where they are in the document. VS Code ranks with its quick-open scorer, a different
  algorithm; one matcher for every list the editor filters is worth the difference in a tie.
- **Preview, accept and cancel are VS Code's.** The selected entry is centred and its range washed whole
  lines at a time, the caret where it was. Enter selects the target (a line's position, a symbol's
  `SelectionRange` start), centres it and records a jump. Escape gives back the view exactly as it was
  when the field opened and the keyboard to the code. Accepting with nothing to go to does nothing.
- **The jump list is VS Code's selection stack, carried through edits as Rider's is.** Every move of the
  primary caret is heard: a place whose top line is ten lines or more from the current entry's is a new
  entry, a nearer one replaces it, a jump is always a new one, and the line a jump started from is
  recorded first. Walking the list moves the caret without recording anything; a new place after going
  back drops what was ahead; fifty places are kept. VS Code keeps each place's line numbers, so a line
  inserted above sends "back" to the wrong line; the places here are carried through every edit as the
  problems are (`CodeEdit.Carry`), which is what Rider's range markers do.
- **Back and forward centre the place only when it is out of view**, as VS Code restores an entry
  (`CenterIfOutsideViewport`).
- **The field floats over the top of the editor, centred**, as VS Code's quick input stands over the
  editor, with the list under it: a page of entries that follows the selected one, as the completion's
  list is, each row the kind's letter in its colour, the name with the matched characters marked, and the
  container muted. The find bar keeps its corner. The keys of the field (↑ ↓ PageUp PageDown Escape) are
  focus-scoped `Shortcut`s around the editor, enabled while the field shows, the mechanism the find bar's
  Escape already uses, and mounted whether it shows or not, so nothing under them moves in the tree; Enter
  is the field's own submit.
- **What reaches the code while the field shows is the field's.** The field takes the keyboard on the
  frame that draws it, and on the web a person who presses ⌃G and types a line number at once types into
  the code before that frame. So the controller routes to the session what still reaches it: text and a
  paste go on the field's text, and Enter, Escape, the arrows, the page keys and Backspace act on the field,
  as the keymap routes a list's keys to the list while it shows (§5). VS Code's quick input opens at once
  and has no such window; this closes it.
- **The list is the field's listbox, as a `Select`'s is.** The field is the anchor of an `Anchored` whose
  panel role is `Listbox`, so on both targets the field is the combobox, the rows are options and the
  selected one is the field's active descendant: a screen reader follows the arrows while the keyboard stays
  in the field. On the web the attributes ride the field's text control, the focused element, which the
  `Anchored` lowering learned here: it put them on the field's shell, where no screen reader follows them.
  On Photon a text field that anchors a listbox is still announced as a text field (#1088). What the field
  says about a line is a polite live region. A deprecated symbol is drawn in the muted ink, where VS Code
  strikes it through: the vocabulary has no strike for text yet (#1089).
- **eqc annotates an empty `[]`** in a local typed as an array, a `List`, an `IList` or an
  `IReadOnlyList` with its item, as it annotated `new List<T>()` (#1086). The session's filter met it: the
  twin wrote `let matches = [];` and TypeScript in strict mode refused it once a branch assigned it.

## Risks / Trade-offs

- The controller learns the view from the component, one build late at worst. → A request computed
  before the first view is known has no view to centre in and asks nothing; the caret's own reveal still
  brings the target into view.
- Two scrolls on an accept: the component's centring and the host's reveal of the caret. → The host's
  reveal is by the least movement, so after the centring it finds the caret in view and moves nothing.
- A symbol list of thousands of entries. → It is filtered in memory on each keystroke by a matcher that
  allocates nothing per entry, and only a page of it is built.
