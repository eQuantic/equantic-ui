# Tasks

## 1. The step

- [x] 1.1 `CodeHistory` records steps of several `CodeEdit`s; a typed step of one edit joins a run as today
- [x] 1.2 `Undo` and `Redo` answer the document, the selection and the replacements they made, in the order
      made
- [x] 1.3 The controller raises `Changed` for each replacement of an undo and a redo, and brings the
      colours and the widths up to date for each
- [x] 1.4 Check: the history's tests (a step of two undone and redone, a run of typing, a step never joining
      a run), and the conformance twins of the code engine executed against .NET

## 2. The outs of a twin

- [x] 2.1 eqc declares a method's outs `any` in TypeScript (#1034), and the out tests say so

## 3. The move

- [x] 3.1 ⌥↑ and ⌥↓ write the passed line on the block's other side and delete it where it was, one step
- [x] 3.2 Check: a problem on a moved line stays on its text through the move, its undo and its redo, and
      the replacements a move, its undo and its redo raise, in order (`CodeLinesTests`); the line twins
      against .NET (`CodeLinesTwinTests`); and the web surface through the real lowering, where ⌥↑ from its
      keydown moves a line and ⌘Z puts it back in one step, two replacements heard each way
      (`code-editor.spec.ts`), which fails against main's twins

## 4. Copilot's first round

- [x] 4.1 A step's replacements are heard once all are made, with the selection the step found and the one
      it left, and an edit a listener makes while it hears them waits for them
- [x] 4.2 The passed line goes in at the start of the line after the block, so a problem of no width at a
      moved line's end stays on it; the document's end is #1059
- [x] 4.3 Check: a listener that edits as it hears a move or its undo, the order every other listener hears,
      the live selections, and a problem of no width at both ends of a moved line and on an empty one
      (`CodeLinesTests`), each failing before its fix

## 5. Documentation

- [x] 5.1 The wiki's CodeEditor page in every language it is written in: what a move does to problems, and
      what `CodeHistory` answers
- [x] 5.2 One ledger entry, a file under docs/ledger/, citing #1031
- [x] 5.3 Archive this change before the merge
