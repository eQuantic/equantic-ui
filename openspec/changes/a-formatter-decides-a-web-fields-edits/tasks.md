Pull request B of the masked field (#1966). It needs `mask-engine-held-to-dotnet` merged, and is proved with a test formatter. A section ends with the check that proves it against the real thing. Its heading names the sub-issue it closes. The shared design is `mask-engine-held-to-dotnet`'s, cited by decision number.

## 8. A formatter decides every edit of a text entry on the web (8 points, #1975)

- [ ] 8.1 The web realizer's C# (`WebLoweringVisitor.LowerTextEntry`) and its twin (`lowering.ts`,
  `lowerTextEntry`).
  - A formatted entry is not prose: `spellcheck`, `autocapitalize`, `autocorrect`.
  - The twin's `beforeinput` handler follows the table of design decision 5.
  - It writes through `execCommand('insertText')` under a guard, and falls back to assigning `value` where
    `execCommand` is absent.
  - It calls `onChanged` itself.
  - Test: `text-entry-formatter.spec.ts` (vitest), driven as the browser drives the field: a `beforeinput` of each
    input type, with its `cancelable` and `isComposing`.
- [ ] 8.2 The composition rule: `compositionstart` keeps the state, `compositionend` applies the committed text
  once, and the `input` after it is ignored.
  - Test: the same spec, with a composition sequence as Chrome sends it and as Firefox sends it.
- [ ] 8.3 The commands: `keydown` `Ctrl`/`Cmd+Z`, `Ctrl`/`Cmd+Shift+Z`, `Ctrl+Y` and `Insert`, and `historyUndo` and
  `historyRedo`, are cancelled and handed to the formatter. `caret-shape: block` is set while `Overwriting`.
  - Test: the same spec, including a formatter that takes no command, for which the browser's own action stands.
- [ ] 8.4 Check against the real thing: a Playwright walk in Chrome and Firefox (`tests/e2e`) over a sample field
  with a test formatter.
  - Typing in the middle keeps the caret where the formatter put it. This fails on the `input`-rewrite road, as
    measured.
  - Backspace and Delete, a paste, and `Meta+Z` then `Meta+Shift+Z`.
  - Chrome's CDP composition.
  - `Insert` and the block caret in Chrome.

## 9. An obscured formatted entry shows its literals over a real password input (5 points, #1976)

- [ ] 9.1 The C# lowering and its twin: an obscured formatted entry is a `type="password"` input holding the whole
  text, with an `aria-hidden`, `pointer-events: none` overlay of the literals.
  - The overlay is placed at each literal's index times the bullet width measured from the input, and follows its
    `scroll` (observed, never sampled). It has the field's fill behind each glyph.
  - Test: a vitest spec over a jsdom-free layout stub for the placement, and the SSR markup test in
    `SurfaceSsrTests`.
- [ ] 9.2 Copy is refused, `autocomplete` is kept, and the overlay survives a re-render without replacing the input
  (focus is kept).
  - Test: the parity fixture, and a vitest case that re-renders while focused.
- [ ] 9.3 Check against the real thing: Playwright in Chrome and Firefox.
  - A literal stays over its cell while the field scrolls.
  - `Meta+C` leaves the clipboard as it was in Chrome.
  - The input is of type `password` and the overlay is absent from the accessibility tree.
- [ ] 9.4 The wiki's `TextInput` page, in the five languages, documents `formatter` and writing an
  `ITextInputFormatter`. A ledger entry under `docs/ledger/` cites #1966.
  - Test: the wiki guards against the change's wiki branch, and `LedgerTests`.
- [ ] 9.5 Archive the change with `openspec archive a-formatter-decides-a-web-fields-edits --yes` before its pull
  request merges.
  - Check: `./scripts/check-openspec.sh`.
