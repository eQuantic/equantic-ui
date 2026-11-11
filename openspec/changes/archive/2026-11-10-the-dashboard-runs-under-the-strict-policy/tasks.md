# Tasks

## 1. The sample

- [x] 1.1 The dashboard sample calls `UseContentSecurityPolicy()` and names no source. Verify: it builds and serves every page with the enforced header

## 2. The fixture

- [x] 2.1 `tests/e2e/tests/content-security.spec.ts`: every page by address and by navigation, failing on any `securitypolicyviolation` and on a page without the enforced policy. Verify: the suite passes against the sample in Chromium
- [x] 2.2 The A/B. Verify: with an inline script without the nonce in the shell, in a disposable worktree, the fixture fails on every route naming `script-src-elem`

## 3. Documentation

- [x] 3.1 The wiki's Security page, in five languages: what the developer writes and what the SDK derives, no policy string. The fixture in the e2e README
- [x] 3.2 One entry for the ledger citing #1270, as a file of its own under `docs/ledger/`
