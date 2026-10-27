## Why

Closes #1119, the first part of slice 6 of docs/CODE-EDITOR-PLAN.md (#603, under the code editor's epic #419). A
file of thousands of lines cannot be read by its outline: no region folds, though the engine has had a fold
contract and an indentation provider since slice 2, and the rows of slice 2b already hide lines under a header
that stays. Nothing in the editor asks the contract, draws a region, or folds one.

## What Changes

For a developer using the SDK, and for the person in the editor it gives them:

- The gutter shows a chevron beside every line that starts a region, pointing down while it is open and right
  while it is folded, and a press folds or unfolds it, as VS Code's does. A folded region keeps its header line
  and hides the lines under it, and a `⋯` after the header's text unfolds it when pressed.
- ⌥⌘[ and ⌥⌘] (Ctrl+Shift+[ and Ctrl+Shift+] on Windows and Linux) fold and unfold the innermost region at the
  caret, ⌘K ⌘[ and ⌘K ⌘] fold and unfold it with every region inside it, ⌘K ⌘L toggles it, ⌘K ⌘0 folds every
  region and ⌘K ⌘J unfolds them all. The commands are `Fold`, `Unfold`, `FoldRecursively`,
  `UnfoldRecursively`, `ToggleFold`, `FoldAll` and `UnfoldAll`, run by an app's menu through `Execute`.
- The regions come from `ICodeFoldProvider`, shaped after LSP's `textDocument/foldingRange` and now asked as
  every other contract is, asynchronously and cancellable, after the text rests. With no provider the
  indentation's is used, as VS Code's "auto" strategy does, so every language folds.
- A fold follows the text: the regions and the folded ones are carried through every edit, an edit that reaches
  inside a folded region unfolds it, and a caret moved into a folded region (by a jump, find or go to line)
  unfolds what hides it. Copying across a fold copies the hidden lines too.
- `CodeEditorController.Folding` holds it for an app: the regions, the folded ones, and the same commands as
  methods, and `CodeEditor.FoldProviders` takes the providers as `SymbolProviders` takes theirs.

The parts it reaches: the engine (`eQuantic.UI.Code`: the contract, the folding session, the commands and the
keymap), the components (`CodeEditor` and `CodeBlock`: the chevrons, the `⋯`, the rows of a folded region), the
runtime's transpiled twins, and the interface strings in every language the SDK speaks. The public surface grows
in Code and Components. One contract changes shape: `ICodeFoldProvider.FoldsFor(document)` becomes
`FoldsAsync(document, cancellation)`, and an app with a fold provider of its own writes it as a task. The
developer surface is unchanged.

## Capabilities

### New Capabilities

- `code-folding`: the regions, folding and unfolding them by key, by the gutter and by the `⋯`, and what a fold
  does to edits, the caret and a copy.

### Modified Capabilities

None.

## Impact

- `src/eQuantic.UI.Code/` (Folding, Editing)
- `src/eQuantic.UI.Components/` (`CodeEditor`, `CodeBlock`, the strings)
- the runtime's transpiled twins
- Tests in the engine, the components on Photon and the conformance suite; the dashboard sample's code screen;
  the wiki's CodeEditor page in five languages.
