# Proposal

Closes #1016, a Bug under #295 (CodeEditor intelligence), and #1059, a Bug under #601 (Editing power): two
places where the code editor kept an answer or a mark past what it could stand for.

## Why

- A complete completion answer was kept for any word, where LSP's `isIncomplete: false` promises only the
  words that extend the one it was asked for. ⌃Space on `Col`, then Backspace: the list went on filtering
  the answer for `Col`, and an entry the server offers for `Co` stayed missing until the list closed.
- `CodeEdit.Carry` pushes an empty range past text inserted at it. A line move at the document's end
  writes the passed line at the end of the last line, at the very place a problem of no width holds, so
  the problem went with the passed line. Enter at a missing semicolon did the same.

## What Changes

- A complete answer is asked again, as `Invoked`, once the word is one it no longer extends, as VS Code
  re-triggers when the caret moves left. A provider whose answer still holds keeps it; the choice the
  issue left open is settled this way so that a word taken back does not ask providers that need not
  answer again.
- An empty range stays in front of an insertion that begins with a line break, which is what writing a
  line below means. Any other insertion pushes it on, as before.

For a developer: nothing to write. A provider is asked again when the word goes back past its answer. No
public surface moves.

## Capabilities

### Modified Capabilities

- `code-completion`: a complete answer holds only for the words that extend its word.
- `code-diagnostics`: a problem of no width stays before a line written below it.

## Impact

- `src/eQuantic.UI.Code/Intelligence/CodeCompletion.cs` and `Document/CodeEdit.cs`, write-once, so both
  targets take them.
