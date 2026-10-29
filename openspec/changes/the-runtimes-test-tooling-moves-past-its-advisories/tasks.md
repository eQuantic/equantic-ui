# Tasks

## 1. The two lockfiles

- [x] 1.1 Raise the floors of `happy-dom`, `vite` and `vitest` and re-resolve `bun.lock` with `bun install --lockfile-only` over the old lock. Verify: only those packages, the `@vitest/*` family and the hoisted entries move, and no second vite appears
- [x] 1.2 Move `source-map-js` and `brace-expansion` in `bun.lock` by version and integrity, their dependencies unchanged. Verify: a second `bun install --lockfile-only` leaves the file byte for byte, and a fresh `bun install --frozen-lockfile --no-cache` installs the patched versions
- [x] 1.3 Refresh `package-lock.json` with npm. Verify: both lockfiles record the same 233 versions

## 2. Proved

- [x] 2.1 `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`. Verify: `tsc` is clean and vitest passes on happy-dom 21
- [x] 2.2 Bundle `Resources/boot.ts` as `BundleRuntime` does, before and after. Verify: `runtime.js` and its map are byte-identical
- [ ] 2.3 Restore Web Animations' handled finished promise in the test environment. Verify: `spreadsheet-read-aloud-web.spec.ts` fails the run with three unhandled AbortErrors on happy-dom 21 alone and passes with the setup file, and `test-environment.spec.ts` fails without the setup file and passes with it

## 3. Documentation

- [x] 3.1 One entry for the ledger citing #1177, as a file of its own under `docs/ledger/`
