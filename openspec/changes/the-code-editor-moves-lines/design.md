## Context

The keymap is data since #1027: a chord names commands tried in order, `Execute` runs one and answers
whether it did anything, and `CanExecute` says whether it would. Every edit goes through one door,
`Edit`, which records ONE replacement and the selection before and after it; `ShiftLines` (Tab over
several lines) is the model of an edit over whole lines, one replacement from the first line's start to
the last line's end.

## Goals / Non-Goals

**Goals:** VS Code's line operations and keys, each one replacement and one undo step, the selection
kept on the lines each moved, the same C# on both targets.

**Non-Goals:** multiple cursors (#1012), where each cursor's lines move; a locale-aware sort.

## Decisions

- **The lines an operation takes** are the selection's, a selection that ends at column 0 leaving that
  line out, the rule `ShiftLines` already applies and every editor shares. With no selection it is the
  caret's line.
- **One replacement each.** A move replaces the moved lines and the one they pass with the two in their
  new order, a copy inserts the lines beside themselves, a delete takes the lines and one line break, a
  join and a sort replace the lines with their result, and an insert writes a line break and an indent.
  One `Edit` each, so one undo step each and one `CodeEdit` for a listener.
- **The selection an edit leaves is the history's.** `Edit` gains the selection to leave, and the history
  records it as the edit's selection after, so redo of a moved line selects the line again instead of
  the caret at the end of what was written. Undo already puts back the selection before.
- **Join as VS Code's `JoinLinesAction`:** the next line's leading white space goes, one space joins the
  two unless the text before ends with white space (collapsed to one space) or is empty, and a next line
  of white space alone adds nothing. With nothing selected the caret stands where the lines met.
- **Sort ordinal, case aside, with an ordinal tie-break.** VS Code sorts with the locale's collator;
  .NET's culture and the browser's `Intl` do not agree on every pair, and the two targets must give the
  same lines. Ordinal ignoring case, ties broken ordinally, is the same on both and puts `apple`,
  `Banana`, `cherry` in the order a person expects.
- **Insert above and below** indent the new line as the caret's line, and below a line that opens a
  block one step more, as `InsertNewLine` does for Enter; the caret goes to the new line's indent.
- **Keys as VS Code's.** ⌥↑ ⌥↓ are named after the panel's own bindings of the same chords, so the panel
  keeps them while it shows overloads. ⌃J joins on a Mac only, as in VS Code, and is bound to ⌃ and ⌘
  both, as ⌃Space is, since the browser reports Ctrl as ⌘. On Windows and Linux, where VS Code binds no
  join, Ctrl+J stays the browser's.

## Risks / Trade-offs

- A host that read ⌘↩, ⇧⌘↩ or ⇧⌘K in an editor loses them. → The baseline names each, and the chords are
  VS Code's own.
- The sort differs from VS Code's on accented letters. → Stated in the spec and the wiki.
