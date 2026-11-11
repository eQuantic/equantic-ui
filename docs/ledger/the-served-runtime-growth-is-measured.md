- **2026-11-10 · The served runtime's growth since #1219 is measured, pull request by pull request**: the runtime
  went over its budget on `main` with no merge stopped by it
  ([#1357](https://github.com/eQuantic/equantic-ui/issues/1357)), so each of the 45 first-parent commits from
  #1219 to 0.2.0-preview.68 was bundled as `BundleRuntime` bundles it and measured as `ServedRuntimeBudgetTests`
  measures it, the method checked against the test's own 324,551 bytes. Seventeen merges touched the runtime and
  added 3,726 bytes of gzip in all, sixteen of them in proportion with what they delivered; #1338 crossed the
  ceiling. The one out of proportion is #1283's 370 bytes of dev-only code, which every production page loads,
  filed as [#1359](https://github.com/eQuantic/equantic-ui/issues/1359). The budget was recorded again by #1365.
  The table is `docs/SERVED-RUNTIME-GROWTH.md`.
