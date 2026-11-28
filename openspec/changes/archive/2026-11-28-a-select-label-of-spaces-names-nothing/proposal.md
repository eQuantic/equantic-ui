# Proposal

Closes #1988, a Bug under #1587.

## Why

The secondary review of #1936 (#1838, a `Select` named by its label) found four things, and the pull request merged before they were fixed:

- a label of nothing but spaces drew an empty line above the field and named the combobox with spaces, and a null label reaching the runtime's twin threw while it built;
- `LabelHidden` could be set only with `new`, while every tree here is written with factories;
- the test of a hidden label read only the semantics, which are the same whether the label is drawn or not, so it could not fail;
- a doc comment was misplaced.

## What Changes

- **A label that is null, empty or only spaces is no label**: it draws nothing above the field and names nothing, on both targets.
- **`UI.Select` takes `labelHidden`**, as `UI.DatePicker` does, and hands it to `LabelHidden`; its twin is transpiled again.
- The test of a named select checks what is drawn above the field as well as what is read, for a label, a hidden one and one of spaces. The parity fixture holds a select labelled with spaces.

The surface changes in one place: `UI.Select` gains a last parameter, `labelHidden`, declared in the unshipped API file that brought `label` (#1838), since neither has shipped.

## Impact

- `eQuantic.UI.Components`: `Select` and `UI`, and the runtime's transpiled twins.
- `openspec/specs/screen-reader-parity`.
