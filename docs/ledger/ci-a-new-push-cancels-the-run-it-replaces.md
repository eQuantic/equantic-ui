- **2026-11-09 · A new push to a pull request cancels the CI run it replaces**: `ci.yml` had no
  `concurrency` block, so every push left the previous head's run going to the end, each holding three
  macOS runners; that afternoon #1251 started six runs, and three other pull requests four each, while
  every other pull request waited three to seven minutes for a macOS runner. A pull request's runs now
  share a group keyed on its number and cancel the one in progress; a run on `main`, on a tag, on a
  merge group or by hand gets a group of its own and is never cancelled
  ([#1309](https://github.com/eQuantic/equantic-ui/issues/1309)). Proposed and archived through
  OpenSpec (`openspec/specs/working-agreement`).
