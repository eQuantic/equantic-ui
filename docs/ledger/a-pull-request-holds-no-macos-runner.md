- **2026-11-10 · A pull request's push holds no macOS runner**: GitHub Free runs five macOS jobs at
  once, every push to a pull request held four of them, and on 2026-11-10 41 runs queued behind them
  while nothing merged and the `v0.2.0-preview.68` tag run waited. The four macOS jobs now run in the
  merge queue, on main, on tags and by hand, and a pull request's run reports them as skipped under
  their check names. The macOS runtimes left the `build-platform-runtimes` matrix to be skipped
  alone, with the legs' steps in a composite action
  ([#1385](https://github.com/eQuantic/equantic-ui/issues/1385)). Proposed and archived through
  OpenSpec (`openspec/specs/working-agreement`).
