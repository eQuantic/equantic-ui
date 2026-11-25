# Tasks

## 1. The import

- [x] 1.1 The harness's import pattern takes `import"./chunk.js"` as it takes `import "./chunk.js"`
- [x] 1.2 `tests/HydrationSite`: `Ribbon`, reached by `ShelfOnePage` and `ShelfTwoPage` only through `Showcase`, and by `RibbonPage` itself
- [x] 1.3 Verify: the shelf page's served module begins with a side-effect import of a chunk; against the harness before the fix the case fails with the issue's `Cannot find module`, and passes after it; the other hydration cases pass as before
