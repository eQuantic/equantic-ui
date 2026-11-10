- **2026-11-09 · The lockfiles Dependabot watches are the trees that run**: GitHub's dependency
  graph reads `package-lock.json` and no `bun.lock`, so the runtime's tests installed from a
  `bun.lock` that Dependabot never read, beside a `package-lock.json` mirror kept in step by hand
  (#107 found it three majors behind), and the extension's Marketplace publish resolved its own
  unpinned vsce tree, in the one job that holds the credential. `RuntimeLockfilesTests` now
  compares the runtime's two lockfiles by resolved `name@version`, and both against the
  manifest's ranges, and fails naming what differs: today they agree on 233 pairs over 228 names,
  and a version moved in one alone, or a range moved in the manifest alone, fails. The publish
  installs the extension's lockfile with `npm ci` and runs the vsce it pinned, and the stray root
  `bun.lock`, which nothing read, is gone
  ([#1180](https://github.com/eQuantic/equantic-ui/issues/1180)). Proposed and archived through
  OpenSpec, with the requirement written into the working agreement's spec.
