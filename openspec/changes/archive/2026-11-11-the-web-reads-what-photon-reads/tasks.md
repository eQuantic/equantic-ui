# Tasks

## 1. A closed tooltip

- [x] 1.1 The hover panel carries an atomic `visibility:hidden` at rest (`BuildAnchorPanel`, `buildAnchorPanel`), and the theme's reveal and suppression rules set `visibility` with `opacity`. Verify: `SemanticsParityTests` without the `Tooltip` entry, and failing with the C# line removed

## 2. A group read row by row

- [x] 2.1 A group's cells carry ids, and `LowerGroupOrder` / `lowerGroupOrder` own them by row and column. Verify: `SemanticsParityTests` without the 18 `case table-view Group` entries

## 3. A field's message

- [x] 3.1 `TextInput` draws its caption in a `LiveRegion`, and the description twin is `hidden` instead of `aria-live` on both realizers. Verify: `SemanticsParityTests` without the text field entries, `text-input.spec.ts`, the shared component fixture and the transpiled `TextInput` regenerated

## 4. The proof on the real thing

- [x] 4.1 The Web suite, the runtime suite and the parity fixture, on the branch

## 5. Documentation

- [x] 5.1 The audit's quotes of the changed lines follow the code, and the unanchored-citation count falls
- [x] 5.2 One entry for the ledger citing #1329, #1331 and #1332, as a file of its own under `docs/ledger/`
