## Context

`CodeHistory` records one `CodeEdit` per step: a run of typing coalesces into one, and every other edit is
a step of its own. `Undo` and `Redo` answer the one range they replaced and the one they wrote, and the
controller raises one `Changed` with them. A line operation (#1033) is therefore one replacement over
every line it touches, and `CodeEdit.Carry`, the rule every problem travels by, takes a range inside a
replacement to the replacement's ends: correct for text that was rewritten, wrong for text that only
moved.

## Goals / Non-Goals

**Goals:** a step that holds several edits; a move whose moved lines keep their problems; listeners that
hear every replacement, through edits, undo and redo alike; the typing run as it is.

**Non-Goals:** several carets (#1012), which this mechanism serves; an edit that knows it MOVED text, which
no listener understands (a language server speaks replacements, as LSP's content changes do).

## Decisions

- **A step is the edits a command made, in the order it made them**, each in the coordinates of the
  document it was made on. Undo takes them back last first, each inverse (its inserted range back to its
  removed text) in the document as the later ones left it; redo makes them again first first. Sequential
  edits are the general case: any command can make them, overlapping or not, and nothing has to be sorted
  or kept apart. A command that edits several places in one pass makes them from the document's end to
  its start, so each is made on text no earlier edit moved.
- **A command makes a step in a scope**, `AsOneStep`, as VS Code's commands push one undo element: its
  edits go through the one door, `Edit`, one after another, each raising `Changed` with the document it
  made and the selection it left, and the scope records what they made as one step. An edit outside a
  scope is a step of its own, as today.
- **`Changed` is raised for each replacement**, through an edit, an undo and a redo, in the order they
  happen, the document being the one that replacement made. A listener already handles one replacement
  at a time; it hears more of them, never a new shape.
- **The selection a step leaves** is the last edit's, and the one it found the first edit's, as for one
  edit today.
- **The typing run coalesces steps of one typed edit**, as today: a step of several edits never joins one.
- **A move is the passed line moving.** ⌥↑ writes the line above the block again below it, then deletes it
  above; ⌥↓ the line below, the other way round. The block is the selection's, which a person is working
  on, so its problems travel with it; the passed line is written anew, and its problems are left empty
  where its text was until a provider answers, as any deleted text's are. It is the smaller side too: one
  line, where the block may be many.
- **The history's shape:** `Record(IReadOnlyList<CodeEdit>)` records a step, `Record(CodeEdit)` a step of
  one; `Undo` and `Redo` answer the new document, the selection, and the replacements they made as
  `CodeEdit`s in the order made, which is what the controller raises and what keeps the colours and the
  widths up to date line by line.

How Flutter solves it: `UndoHistory` stores whole `TextEditingValue`s, a snapshot per step, so it never
replays replacements and never tells anyone what changed. A code editor's listeners need the
replacements, and a snapshot of a 50,000-line file per keystroke is what an editor cannot keep, so the
history here stays a history of edits, as VS Code's (`EditStack`, a list of single edits per element) and
Monaco's undo elements are.

## Risks / Trade-offs

- A problem on the passed line goes empty until it is answered again. → Stated in the spec and the wiki;
  a provider answers after the pause, and a problem the app reports itself is the app's to report again.
- More `Changed` events per command. → Two for a move, one for everything else; the listeners' work is
  per replacement already.
