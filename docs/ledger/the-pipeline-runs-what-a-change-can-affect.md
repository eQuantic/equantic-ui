- **2026-11-11 · The pipeline runs what a change can affect, and a push to main cancels the run it
  replaces**: since the merge queue, every merge is a push to main whose run repeats the suite the
  queue had just run on the same tree, and on 2026-11-11 a merge group waited for macOS behind twelve
  of those runs, eleven already superseded. A push to main now cancels the run of the push before it.
  And a `changes` job classifies each pull request and merge group with `scripts/ci-changes.sh`: a
  change to documents runs only the test groups that read documents (`web` and `rest`) and the
  guards, a change to the VS Code extension only builds the extension, and anything else, or anything
  the classifier cannot read, runs everything. The 18 required checks report under their names
  whatever runs ([#1421](https://github.com/eQuantic/equantic-ui/issues/1421)).
