## Why

This change ships pull request B of #1966 (under #898), through sub-issues #1975 and #1976. With the vocabulary and
the engine of `mask-engine-held-to-dotnet` in place, the web's text entry must hand every edit, composition and
command to its formatter, and show its answer with the caret where the formatter put it. An obscured formatted field
must keep the literals visible without losing a password field's protections.

## What Changes

For a developer, a `TextInput(…, formatter: …)` now behaves on the web:

- **Edits:** every edit goes through the formatter. The caret lands where the formatter puts it, and Backspace over
  a literal is the formatter's.
- **Compositions:** a composition shows as the browser draws it, and its commit is one edit.
- **Commands:** `Ctrl`/`Cmd+Z`, `Ctrl`/`Cmd+Shift+Z`, `Ctrl+Y` and `Insert`, and the browser's Edit menu, context
  menu and iOS gestures reach the formatter's undo, redo and overwrite. The field writes its text with
  `execCommand('insertText')`, which keeps those dispatching (measured in Chrome 157 and Firefox 159).
- **Overwrite** draws a block caret where the browser can (Chrome).
- **Not prose:** a formatted field is served with no spell-check, autocorrection or capitals.
- **Obscure:** an obscured formatted field is a real `type="password"` input, with the formatter's literals drawn
  over their bullets.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `text-entry`: on the web,
  - a formatter decides every edit;
  - a composition is never formatted while composed;
  - a formatted entry is not prose;
  - the targets' undo, redo and Insert reach the formatter;
  - an obscured formatted entry shows its literals.

## Impact

- **Parts reached:** the web realizer (`WebLoweringVisitor.Text`) and its twin (`lowering.ts`), the parity fixture,
  the e2e tests, and the wiki's `TextInput` page.
- **No public surface moves** (no `PublicAPI` line), and the developer surface does not move.
- **No break.**
- **Principles:** 4, 6, 9 and 11 are followed (the twin is runtime TypeScript). One is bent: on the web, an
  obscured field's literals stand in bullet cells, which the owner accepted for 1.0.
