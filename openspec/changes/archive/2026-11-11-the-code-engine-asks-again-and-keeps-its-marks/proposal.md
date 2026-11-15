# Proposal

Closes #1016 and #1059, Bugs of the code engine, and #1298, a test that failed now and then on CI.

## Why

- **A complete answer was filtered past its promise (#1016).** A provider's complete answer covers every
  word that extends the word it was asked for, which is what LSP's `isIncomplete: false` promises, and no
  other. ⌃Space at the end of `Col`, then Backspace, and the list went on filtering the answer for `Col`:
  an entry the provider offers for `Co` (`Count`) was missing until the list closed.
- **A problem of no width went with a line written after it (#1059).** An empty range at an insertion
  point is carried past the inserted text. A move that passes the document's last line writes that line
  after the block's last line, at the very place a missing semicolon's problem holds, and the problem went
  to the passed line's end. Enter typed where the semicolon is missing did the same.
- **A diagnostics test raced its own dispatcher (#1298).** It waited for the answer's post by waiting for
  the queue to hold anything, and the queue already held what the editor posted as it was made, so when
  the answer's continuation came after the await, the drain ran without it.

## What Changes

- The completion asks again a provider whose complete answer's word the word typed no longer extends, as
  the list opened (a list a trigger character opened is asked as invoked), and filters its answer
  meanwhile. Only those providers are asked, not every one as VS Code does on a move left: an answer for a
  word the word typed still extends is still the whole of what its provider offers.
- A problem of no width at an insertion point stays in front of the insertion when the inserted text
  begins with a line break: a line written below the place is no text of the place's. A snippet's stop, a
  caret and a place to go back to still follow what is typed at them.
- The test drains what the editor posted as it was made before it waits for the answer.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `code-completion`: a complete answer is asked for again once the word leaves it
- `code-diagnostics`: a problem of no width stays in front of a line written at it

## Impact

The code engine (`CodeCompletion`, `CodeEdit`), write-once, so both targets. No API.
