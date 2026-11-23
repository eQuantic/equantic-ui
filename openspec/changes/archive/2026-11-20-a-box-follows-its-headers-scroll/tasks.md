## 1. Vocabulary

- [x] 1.1 `BoxStyle.Scrolled` (a `StyleDiff?`), documented beside `Hover`, `Focus` and `Pressed`, declared in Primitives' `PublicAPI.Unshipped` for the branch
- [x] 1.2 Check: the Primitives build passes with the analyzer's RS0016 satisfied

## 2. Web realizer

- [x] 2.1 `StyleSink.HeaderScrolled` and its selector in `StateSelector`, weighing (0,1,1), never media-gated
- [x] 2.2 `LowerBox` appends the diff to the family, over the box's base, through `DiffDeclarations`
- [x] 2.3 Tests: every member lands in the family; the selector's weight sits between the resting class and the hover, measured with the specificity reader; a nested header is excluded; each seen failing without the change

## 3. Runtime

- [x] 3.1 The `BoxStyle` twin and `BoxStyleValue` carry `scrolled`
- [x] 3.2 `HEADER_SCROLLED` and its selector in `stateSelector`, byte-identical to the C# twin
- [x] 3.3 `lowerBox` appends the diff and installs the scroll controller
- [x] 3.4 The parity fixture gains a header whose boxes declare `Scrolled`, replayed by the vitest twin
- [x] 3.5 Tests: the vitest spec of the family, and a header with no `ScrolledStyle` of its own gets its attribute; each seen failing without the change. The nested header is left to the browser: happy-dom matches no complex selector inside `:where()`

## 4. Photon

- [x] 4.1 `PressScope.InScrolledHeader`, set by the pinned header's visit for its subtree and restored after
- [x] 4.2 `EffectiveStyle` lays `Scrolled` first, under hover, focus and press
- [x] 4.3 Tests: the box draws its diff while its header's scroll view is past the threshold, glides under its `Transition`, keeps its resting style in no header and under an unscrolled inner header, and a hover beats it; each seen failing without the change

## 5. Proof against the real thing

- [x] 5.1 A real browser: the dashboard's `/header` page in Chromium (`tests/e2e/tests/header-scrolled.spec.ts`), the computed opacity and transform of each layer read before and after the page scrolls, a hover beating the scrolled fill, and a list's own pinned header following the list; seen failing without the nested-header exclusion

## 6. Documentation

- [x] 6.1 The wiki's styling page, in its five languages, on the branch of the wiki named like this one
- [x] 6.2 A ledger entry under `docs/ledger/`, citing #1769
