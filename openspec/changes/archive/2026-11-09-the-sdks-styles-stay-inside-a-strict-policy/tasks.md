# Tasks

## 1. Measured

- [x] 1.1 A page under a strict style policy in Chromium 152, case by case. Verify: the table in the design
- [x] 1.2 The style attributes the dashboard's server-rendered pages carry. Verify: 4 to 25 a page, on every page, from six sites in each realizer

## 2. The styles

- [x] 2.1 `style-src 'nonce-…'` and `style-src-attr 'unsafe-inline'`. Verify: the strict policy's exact text
- [x] 2.2 The nonce on stylesheet links. Verify: a stylesheet `HtmlTag` and a `StylesheetAsset` render with it, a preconnect link without it, and a served page's every stylesheet link carries it
- [x] 2.3 `createStyleElement` in the runtime, for `eq-atomic` and `eq-faces`. Verify: `page-nonce.spec.ts` fails without the stamping and passes with it
- [x] 2.4 The vector container through `style.cssText`. Verify: no `setAttribute('style'` in the runtime

## 3. Documentation

- [x] 3.1 The wiki's ServerIntegration page, in five languages
- [x] 3.2 One entry for the ledger citing #1268, as a file of its own under `docs/ledger/`
