- **2026-11-21 · The shared-library sample's check follows the factory**: `samples` was red on main from
  #1793 on, and on the tag `v0.2.0-preview.71`, whose run published nothing. The sample composes the
  Common project's `Wordmark` with its factory since #818, so the page calls `AppUI.wordmark()` and the
  app's factory module imports the component, and `check-shared-library-sample.sh` still asked the page
  to import it. The check now asserts each link of that path, and a copy of the build's output with any
  one of them removed fails, naming it. A pull request's run skips the macOS jobs, `samples` among them,
  which is how the stale check reached main
  ([#1798](https://github.com/eQuantic/equantic-ui/issues/1798)).
