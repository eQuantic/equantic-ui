# Proposal

Closes #420, a User Story under the feature #419 (the code editor of `docs/CODE-EDITOR-PLAN.md`),
itself under the epic #198 (The tracks in flight).

## Why

The engine of #386 answers what differs between two texts, and nothing in the SDK could show it: an
IDE comparing a file with its last commit, a review of a change, or a page quoting a patch had no
component to draw two versions side by side or inline, fold what did not change, and step through
what did. Slice 2b of the plan is that view, and its design is the plan's ninth section, "A row is
not a line": a view draws ROWS, some of which are lines of its document and some of which are not.

## What Changes

- `CodeDiff`, a write-once component: two texts, or one file of a patch (`CodeDiff.OfPatch`), side
  by side with the two sides level at every change, or inline with the removed lines between the
  lines that replaced them. A changed line is washed across its row and its changed words are
  marked. A run of unchanged lines longer than `Context` folds into one row that opens on a press.
  F7, Shift+F7 (and Alt+F5) and the toolbar step through the changes. Each side is numbered as its
  file is. The modified side edits and is compared again after every edit (`OnChanged`), and
  `Editor` is its controller for an IDE.
- The engine maps a view's lines to its rows (`CodeRows`, `CodeFiller`, `CodeCollapse`, `CodeRow`),
  lays out each side (`CodeDiffLayout`), opens a source from two texts, two documents or a patch
  (`CodeDiffSource`), and reads a unified diff (`CodePatch`). `CodeGrid` takes the rows, and
  `CodeBlock` draws them.
- `Shortcut.FocusScoped`: a chord answered only while the keyboard focus is inside its subtree. The
  code editor's ⌘F and the diff's keys use it, so of two on one page the one in use answers.
- The macOS shell names F1 to F20, PageUp and PageDown (`MacKeys`), so a chord written F7 matches.
- eqc translates, as .NET answers, the shapes this code was the first to cross: an extended
  property pattern, a string holding a line break, a negative declared default, and a record's
  nullable delegate and decimal annotations.

A break, allowed in preview: `CodeGrid` takes the rows as a third member, `CodeBlock.Gutter` an
optional numbering, and Photon's `ShortcutBinding` a scope, so a compiled call to their old shapes
fails where a source call still compiles. `CodeEditor`'s ⌘F answers only while the keyboard is in
the editor. Migration line: rebuild against the new shapes, no source change needed.

## Capabilities

### New Capabilities

- `code-diff`: two versions of a text and what differs between them, drawn as rows.
- `keyboard-shortcuts`: a chord's scope, and a key's name on every host.
- `csharp-translation`: the translation rules this change settled, each against .NET's answer.

### Modified Capabilities

None.

## Impact

eqc (`PatternConverter`, the record emitter, the one string-literal writer), the code engine
(`eQuantic.UI.Code`), the component library (`CodeDiff`, `CodeBlock`, `CodeEditor`, `SdkStrings`),
the vocabulary (`Shortcut`), the runtime (the twins, the shortcut controller, the code surface's
press), the Photon host (`KeyDown`), the macOS shell, and the dashboard sample (`/diff`). The public
surface moves (the new types above and the three breaks), and the developer surface does not.
