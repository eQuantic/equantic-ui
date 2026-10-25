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
  edits go through the one door, `Edit`, one after another, each bringing the colours and the widths up to
  date as it is made, and the scope records what they made as one step, then raises them. An edit outside
  a scope is a step of its own, as today.
- **`Changed` is raised once per change, with its replacements** (`CodeChange`), through an edit, a step,
  an undo and a redo, once all are made, in the order made, each with the selection the change found and
  the one it left, as VS Code raises one content change event with all of an operation's changes once the
  model holds them, and as a language server's `didChange` carries an array. Raised as each was made, a
  listener that edited in between moved the text the next replacement's range had been taken from, and
  that replacement deleted the wrong line (Copilot's first round on #1058). Raised one at a time once the
  step was made, a listener that read the text while it heard the first reported a problem against the
  step's result, which the rest of the step then carried off it (the second round). A change a listener
  makes while it hears one waits for it, and every listener hears it after it. The event's shape moves
  from one `CodeEdit` to a `CodeChange`, and a document replaced whole, which was null, is a change with
  no edits, `Replaced`.
- **The editor's own problems follow each replacement the moment the document takes it**, as the colours
  and the widths do, and the change, once heard, only says that they moved and asks the providers again.
  While a change is being heard, a change a listener made is already in the document and waits to be heard
  after it, so a range read from the document then is ahead of the changes heard so far: carried when its
  change was heard, a problem reported against that text went through the waiting change a second time
  (Copilot's third round on #1058). VS Code's markers are the same: decorations the model moves as it
  applies an edit, never through its content event.
- **The selection a step leaves** is the last edit's, and the one it found the first edit's, as for one
  edit today.
- **The typing run coalesces steps of one typed edit**, as today: a step of several edits never joins one.
- **A move is the passed line moving.** ⌥↑ writes the line above the block again below it, then deletes it
  above; ⌥↓ the line below, the other way round. The block is the selection's, which a person is working
  on, so its problems travel with it; the passed line is written anew, and its problems are left empty
  where its text was until a provider answers, as any deleted text's are. It is the smaller side too: one
  line, where the block may be many. The passed line goes in at the start of the line after the block and
  comes out from the start of its own, with its line break, so neither replacement writes where the
  block's lines start or end, and a problem of no width there (a missing semicolon at a line's end) stays
  on them: written after the block's last line, the passed line took that problem along (Copilot's first
  round on #1058).
- **The history's shape:** `Record(IReadOnlyList<CodeEdit>)` records a step, a step of one edit included,
  since no overload crosses to the twin; `Undo` and `Redo` answer the new document, the selection, and the replacements they made as
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
- At the document's end no line follows the block, and the passed line is written after its last line,
  where `CodeEdit.Carry` pushes a problem of no width past the insertion: it goes with the passed line.
  → The carry rule decides it, not the move, and typing meets the same rule, so it is a change of its own
  (#1059).
