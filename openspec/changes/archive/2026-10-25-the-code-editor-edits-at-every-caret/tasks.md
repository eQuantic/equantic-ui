# Tasks

## 1. The model

- [x] 1.1 The carets: the primary as the one selection was, the others beside it in document order, each with
      the cell it aims at; `Selections` to read and place them; made one where they meet; `Selection` the
      primary, setting it one
- [x] 1.2 A step restores every selection: the history keeps a `CodeStep`, its edits and every selection on
      each side; typing at several carets one run
- [x] 1.3 Every command at every caret: the command written for a caret run from the last caret to the first
      as one step, the carets placed in one linear pass; the line commands once for each block
- [x] 1.4 Check: every single-caret test, the seeded property test of #1033 and the keymap's behaviour
      baseline, its diff read; the engine's tests for several carets through every command, and a seeded
      test of every command at carets on lines of their own

## 2. Adding and removing carets

- [x] 2.1 `AddCursor`: a caret on the line above or below every caret
- [x] 2.2 `AddNextOccurrence`, `SelectAllOccurrences`: the word first, then whole word and case; wrapping
- [x] 2.3 `RemoveSecondaryCursors`, and Escape's order (list, card, panel, carets, the trap)
- [x] 2.4 The pointer: ⌥ and a press adds or takes away a caret; ⇧⌥ and a press or a drag selects a column
- [x] 2.5 The keys in both default tables
- [x] 2.6 Check: the engine's key and pointer tests, and the keymap's baseline regenerated and its diff read

## 3. Every command at every caret

- [x] 3.1 Typing, the pairs, Enter, Backspace and Delete (character, word, to the line's start)
- [x] 3.2 Tab, ⇧Tab and ⌘/ over every caret's lines, an edit a line
- [x] 3.3 The line operations over every caret's lines, by block
- [x] 3.4 Copy, cut and paste: a line a selection, and a paste spread over as many carets
- [x] 3.5 The completion's entry at every caret; the panel and the card at the primary
- [x] 3.6 An input method composes at the primary, the other carets carried, and its commit typed at every
      caret
- [x] 3.7 Check: the code twins executed against .NET over seeded sessions that add carets and edit with them
      (`CodeCursorsTwinTests`)

## 4. Drawing

- [x] 4.1 `Carets`, `SelectionsIn`, `CaretLinesIn`, `SelectionBandsIn`, and the block's `ActiveLines` and
      `Selections`
- [x] 4.2 The web surface's input keyed, so it is never made anew as the carets change in number
- [x] 4.3 Check: the web surface through its keydown, and its input kept through more carets and a note
      (`code-editor.spec.ts`), failing without the key; the dashboard's code editor in a browser with ⌘D,
      typing at three carets, ⌘Z, ⌥⌘↓, Escape, ⌥ and a press, and ⇧⌥ and a press

## 5. A twin's list of tuples

- [x] 5.1 eqc annotates an empty list of tuples with the tuple of its elements' names in TypeScript (#1063),
      and `CollectionConstructionTests` says so, failing on main's eqc

## 6. Documentation

- [x] 6.1 The wiki's CodeEditor page in every language it is written in
- [x] 6.2 docs/CODE-EDITOR-PLAN.md: the row of slice 2's third part
- [x] 6.3 One ledger entry, a file under docs/ledger/, citing #1012
- [x] 6.4 Archive this change before the merge
