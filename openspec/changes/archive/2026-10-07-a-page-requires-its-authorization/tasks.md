# Tasks

## 1. The page's route

- [x] 1.1 `[Page]` and `MapPage<T>` routes carry the page's authorization as endpoint metadata
- [x] 1.2 A refused navigation is answered 401 or 403 by the navigation result handler
- [x] 1.3 The runtime and the page modules allow anonymous access
- [x] 1.4 Pinned by `PageAuthorizationTests` over the real pipeline, with a scheme that challenges by
  redirect: anonymous, forbidden, allowed, navigation, `MapPage<T>` and a fallback policy
- [x] 1.5 A registered 404 or 500 page is asked its requirement as its own route asks it, the
  fallback policy and the request as the resource included, pinned by `PageAuthorizationTests`

## 2. The router

- [x] 2.1 The page-state fetch reads a marked 401 or 403 as a refusal
- [x] 2.2 The boot loads a refused route in full instead of rendering the page
- [x] 2.3 Pinned by `page-state.spec.ts`

## 3. Documentation

- [x] 3.1 The wiki's Security page in English and Portuguese, and one `docs/LEDGER.md` line citing #673
