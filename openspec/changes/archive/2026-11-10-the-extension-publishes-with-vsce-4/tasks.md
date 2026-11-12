# Tasks

## 1. The tool

- [x] 1.1 `@vscode/vsce` to `^4.1.0`, lockfile resolved again. Verify: `npm audit` lists no `glob`, and `npm run package` writes a `.vsix` that `verify-package` accepts

## 2. The publish

- [x] 2.1 `publish-extension` publishes with `--oidc`, reads no secret and installs with `--ignore-scripts`. Verify: the job's text, and the check below on it
- [x] 2.2 `check-publish-packages.sh` reads the job, with a control per case. Verify: the script passes, and each control fails the case it guards

## 3. Documentation

- [x] 3.1 The extension's README says what publishing needs: a Trusted Publishing policy, no credential
- [x] 3.2 One entry for the ledger citing #1271, as a file of its own under `docs/ledger/`
