# Tasks

## 1. Hydration records what it wrote over

- [x] 1.1 The reconciler records text, attribute and element differences; classes as a set; markers skipped
- [x] 1.2 Every component's `hydrate` returns its result; the boot writes `window.__EQ_HYDRATION__`
- [x] 1.3 Proved: `reconciler.spec.ts` (each kind, a blank label, nothing where the two agree)

## 2. The router and the server choose one page (#1778)

- [x] 2.1 `routerDisagrees`, at load and after a navigation; the payload names the server's page
- [x] 2.2 Proved: `page-agreement.spec.ts`, `ClientNavigationStateTests`, and the fixture site's rewritten
  path in development and in production

## 3. A served page hydrated in a test (#1775)

- [x] 3.1 The harness, bundled with happy-dom by the embedded bun, carried by the assembly
- [x] 3.2 `HydrationAssert.PageAsync`, the report, the exception; Kestrel on loopback
- [x] 3.3 `ResolveBunPath` returns the bun; the package's buildTransitive targets write it beside the tests
- [x] 3.4 `tests/HydrationSite`: a clean page and one page per defect; `HydrationAssertTests` fails each with
  a message naming it, and passes the clean one
- [x] 3.5 No false positive on the dashboard sample (every page, two widths)

## 4. An app of pages alone builds

- [x] 4.1 The generated-sources prune skips an empty list; proved by the fixture site's build

## 5. Documentation

- [x] 5.1 The wiki's Testing and ServerIntegration pages, in all five languages
- [x] 5.2 `docs/ledger/a-page-hydrates-in-its-tests.md`
