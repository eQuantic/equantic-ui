# Tasks

## 1. The lockfile

- [x] 1.1 Raise the `fast-uri` override to `^3.1.9` and refresh `undici`, `markdown-it`, `fast-uri` and `brace-expansion` with `npm update --package-lock-only`. Verify: the lockfile's diff moves those four packages and nothing else

## 2. Proved by the job that reads it

- [x] 2.1 `npm ci` and `npm run package`, as `package-extension` runs them. Verify: `npm ls` resolves the four patched versions, and `verify-package` finds the design host in the `.vsix` and speaks to it

## 3. Documentation

- [x] 3.1 One entry for the ledger citing #1178, as a file of its own under `docs/ledger/`
