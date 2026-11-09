# Proposal

Closes #1180, a Task under #306.

## Why

GitHub's dependency graph parses `package-lock.json` and does not parse `bun.lock`, so two of the
trees that actually run were not the ones Dependabot watches.

- The runtime keeps two lockfiles for one manifest. `TestRuntime`, and CI's `test-runtime` job
  through it, install from `src/eQuantic.UI.Runtime/bun.lock` with `--frozen-lockfile`. Dependabot
  reads `src/eQuantic.UI.Runtime/package-lock.json` beside it, which nothing installs from and which
  was kept in step by hand. #107 found it three majors behind its manifest, so an advisory was
  reported closed while the tree CI ran still carried it.
- The extension's `publish-extension` job ran `npx @vscode/vsce@^3.9.2`, which reads no lockfile:
  the tree that holds the Marketplace credential was resolved at publish time, unpinned, and was
  neither the one `package-extension` proved nor the one Dependabot watches.
- A `bun.lock` at the repository's root recorded `@ampproject/remapping` for a workspace with no
  `package.json`. Nothing read it.

## What Changes

- `bun.lock` stays the runtime's authority, and `package-lock.json` stays as its mirror, because it
  is the file Dependabot reads. `RuntimeLockfilesTests`, in the Web tests, compares the two by
  resolved `name@version` (bun and npm hoist the same tree differently, so a path decides nothing),
  and compares the ranges each records with every dependency section of the manifest. A change that updates one lockfile
  alone, or moves a range in `package.json` that one tool alone resolved, fails, naming what differs
  and the command that resolves it. Measured on today's main, the two lockfiles resolve the same 233
  `name@version` pairs over 228 names.
- `publish-extension` checks out the extension's `package.json` and `package-lock.json` alone,
  installs them with `npm ci`, the install `package-extension` ran, and publishes with the vsce that
  install put in `node_modules/.bin`. The archive it publishes is still the one the packaging job
  verified.
  The install runs the tree's install scripts, so the step blanks the Marketplace credentials the
  job reads, and the checkout keeps no token in `.git/config`.
- The root `bun.lock` is removed.
- Nothing a developer using the SDK writes or meets changes, and neither the public surface nor the
  developer surface moves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `working-agreement`: a requirement that the lockfile Dependabot watches is the tree that runs, for
  the runtime's tests and for the extension's publish.

## Impact

`tests/eQuantic.UI.Web.Tests/RuntimeLockfilesTests.cs`, `.github/workflows/ci.yml`
(`publish-extension`), and the removal of the root `bun.lock`. Draft pull request #1184 edits both of
the runtime's lockfiles: whichever of the two merges second keeps this guard green, which it does by
updating both lockfiles in the same change.
