## 1. Vocabulary

- [ ] 1.1 `BoxStyle.Scrolled` (a `StyleDiff?`), documented beside `Hover`, `Focus` and `Pressed`, declared in Primitives' `PublicAPI.Unshipped` for the branch
- [ ] 1.2 Check: the Primitives build passes with the analyzer's RS0016 satisfied

## 2. Web realizer

- [ ] 2.1 `StyleSink.HeaderScrolled` and its selector in `StateSelector`, weighing (0,1,1), never media-gated
- [ ] 2.2 `LowerBox` appends the diff to the family, over the box's base, through `DiffDeclarations`
- [ ] 2.3 Tests: every member lands in the family; the selector's weight sits between the resting class and the hover, measured with the specificity reader; a nested header is excluded; each seen failing without the change

## 3. Runtime

- [ ] 3.1 The `BoxStyle` twin and `BoxStyleValue` carry `scrolled`
- [ ] 3.2 `HEADER_SCROLLED` and its selector in `stateSelector`, byte-identical to the C# twin
- [ ] 3.3 `lowerBox` appends the diff and installs the scroll controller
- [ ] 3.4 The parity fixture gains a header whose boxes declare `Scrolled`, replayed by the vitest twin
- [ ] 3.5 Tests: the vitest spec of the family, and a header with no `ScrolledStyle` of its own gets its attribute; each seen failing without the change

## 4. Photon

- [ ] 4.1 `PressScope.InScrolledHeader`, set by the pinned header's visit for its subtree and restored after
- [ ] 4.2 `EffectiveStyle` lays `Scrolled` first, under hover, focus and press
- [ ] 4.3 Tests: the box draws its diff while its header's scroll view is past the threshold, glides under its `Transition`, keeps its resting style in no header and under an unscrolled inner header, and a hover beats it; each seen failing without the change

## 5. Proof against the real thing

- [ ] 5.1 A real browser: the lowered page with a header and two layers, scrolled past the threshold, the computed opacity and transform of each layer read before and after, and a hover beating the scrolled fill

## 6. Documentation

- [ ] 6.1 The wiki's styling page, in its five languages, on the branch of the wiki named like this one
- [ ] 6.2 A ledger entry under `docs/ledger/`, citing #1769
