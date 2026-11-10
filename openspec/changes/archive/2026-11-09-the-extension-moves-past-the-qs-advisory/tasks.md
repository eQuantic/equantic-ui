# Tasks

## 1. Measured

- [x] 1.1 Find what holds each package. Verify: `qs` through `typed-rest-client` (`^6.9.1`), and `minimatch` directly and through `glob`, both from `@vscode/vsce` 3.9.2 (`^10.2.2`)
- [x] 1.2 Read whether a released vsce accepts `minimatch` 11. Verify: 4.1.0, the newest, asks for `^10.2.6`, and no 10.x release is patched

## 2. The update

- [x] 2.1 `npm update qs --package-lock-only` in `extensions/vscode`. Verify: only `qs` moves, 6.15.3 to 6.17.1
- [x] 2.2 `npm audit` on main's lockfile and on this one. Verify: `qs` leaves the report, and nothing else changes
- [x] 2.3 `npm ci` and `npm run package`. Verify: the `.vsix` is written and its host answers

## 3. Documentation

- [x] 3.1 One entry for the ledger citing #1259, as a file of its own under `docs/ledger/`
- [x] 3.2 Write on #1259 what `minimatch` waits for
