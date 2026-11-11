# Tasks

## 1. Hydration takes the client's attributes and text

- [x] 1.1 `Reconciler.hydrate` writes the client's text over a text node the server wrote otherwise, and diffs every adopted element's attributes against the client's with `updateAttributes`, keeping the `data-eq-*` markers the client does not name. Verify: four cases in `reconciler.spec.ts` (a button the server disabled and the client enables is the same element, enabled; `href` and `aria-current` written; a text corrected; a marker kept)
- [x] 1.2 The A/B. Verify: three of the four cases fail on the old reconciler, and the marker case passes on both
- [x] 1.3 The sample. Verify: `/recovery-codes` of the dashboard sample, gated on `IFileSaver.IsAvailable`, served `disabled` and enabled after hydration in a browser, and still `disabled` with the old runtime

## 2. Documentation

- [x] 2.1 The wiki's Capabilities page, "Rendering on the server", says what hydration does with an attribute the server decided, in five languages
- [x] 2.2 One entry for the ledger citing #1302, as a file of its own under `docs/ledger/`
