# Tasks

## 1. Measured

- [x] 1.1 List what reads each lockfile. Verify: `TestRuntime` installs from the runtime's `bun.lock` with `--frozen-lockfile`, nothing installs from its `package-lock.json`, and nothing reads the root `bun.lock`
- [x] 1.2 Compare the runtime's two lockfiles on today's main. Verify: the same 233 `name@version` pairs over 228 names, and the same ranges as the manifest

## 2. The runtime's mirror is compared

- [x] 2.1 Add `RuntimeLockfilesTests`. Verify: both cases pass on today's main
- [x] 2.2 Move `vitest` in `package-lock.json` alone. Verify: the first case fails naming `vitest@4.1.11` only in `bun.lock` and `vitest@4.0.0` only in `package-lock.json`
- [x] 2.3 Move the `vitest` range in `package.json` alone. Verify: the second case fails, saying `bun.lock` was not resolved from the manifest as it is

## 3. The extension publishes the tree that was proven

- [x] 3.1 Check out the extension's manifest and lockfile in `publish-extension`, install them with `npm ci`, and publish with the vsce that install put in `node_modules/.bin`. Verify: `actionlint` reports nothing it did not report before
- [x] 3.3 Blank the Marketplace credentials during the install, whose scripts run, and keep no token from the checkout (`persist-credentials: false`). Verify: `actionlint` reports nothing new
- [x] 3.2 Run the job's checkout, install and vsce on a copy of those two files. Verify: `vsce --version` answers 3.9.2, and `vsce publish --help` lists `--packagePath` and `--azure-credential`

## 4. The stray lockfile

- [x] 4.1 Remove the root `bun.lock`. Verify: nothing in the repository names it

## 5. Documentation

- [x] 5.1 One entry for the ledger citing #1180, as a file of its own under `docs/ledger/`
