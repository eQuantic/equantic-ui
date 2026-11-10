# Tasks

## 1. Measured

- [x] 1.1 Read what `@azure/identity` asks for in each release since the one the lockfile resolved, and
  what `@vscode/vsce` 3.9.2 accepts. Verify: 4.14.1 (npm's `latest`, 2026-10-29) asks for
  `@azure/msal-node: ^7.1.0`, its changelog says so, and vsce 3.9.2 asks for `@azure/identity: ^4.1.0`

## 2. The lockfile

- [x] 2.1 `npm update @azure/identity --package-lock-only` in `extensions/vscode`. Verify: the lockfile
  resolves `@azure/identity` 4.14.1 and `@azure/msal-node` 7.2.1, and every package that moved belongs
  to `@azure/identity`'s own tree

## 3. Against the real thing

- [x] 3.1 `npm audit` on main's lockfile and on this one. Verify: main reports two high advisories,
  `@azure/msal-node` under GHSA-xcx6-jx8p-x3h5 and the `@azure/identity` that carries it; this one
  reports none, and the four moderate ones are the same on both (#1259 has the two Dependabot raises)
- [x] 3.2 `npm ci` and `npm run package`, as CI's `package-extension` job runs them. Verify: msal-node
  7.2.1 installed, `equantic-ui.vsix` packaged (159 files, 11.5 MB) and `verify-package` reports the
  packaged host answers

## 4. Documentation

- [x] 4.1 One entry for the ledger, citing #1179, under `docs/ledger/`
- [x] 4.2 The change archived before the merge
