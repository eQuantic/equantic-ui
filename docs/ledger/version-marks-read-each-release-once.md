- **2026-11-09 · The version-mark guard reads each release once, for every mark**: the guard asked
  git once per mark, a pickaxe search over every ref, and CI's checkout fetches each ref at depth
  one, so every search read about three hundred whole trees. 159 marks took 34 to 39 minutes on
  CI's runners and then more than the hour CI gives a case, which failed both `test` jobs of every
  pull request, and of main, after `0.2.0-preview.67`. It now compares each release with the one
  before it and counts every mark's symbol in the same pass, 67 diffs in all, answering the first
  release whose files hold it: six seconds on a laptop, the answer the old searches gave on CI's
  checkout for all 154 searches, and the one they gave a full clone for all but a name that never
  reached a release. CI fetches the release tags alone, and its hang timeout is fifteen minutes
  again, where the slowest other case took 55 seconds
  ([#999](https://github.com/eQuantic/equantic-ui/issues/999)). Proposed and archived through
  OpenSpec, with no spec moved: the change is in the test suites and CI alone.
